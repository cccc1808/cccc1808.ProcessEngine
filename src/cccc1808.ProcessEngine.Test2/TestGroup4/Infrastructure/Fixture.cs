using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.ClassifierModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Wakeup;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Wakeup;
using cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.InboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.OutboxModule.Services;
using cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services;

using Confluent.Kafka;

using DotNet.Testcontainers.Configurations;

using Microsoft.Extensions.DependencyInjection;

using Testcontainers.Kafka;
using Testcontainers.PostgreSql;

using Xunit.Sdk;

namespace cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class FixtureCollection : ICollectionFixture<FixtureCollection.Fixture>
    {       
        public const string Name = "FixtureCollection 4";
        public const string TriggerQueue = "trigger_events";
        public const string InboxQueue = "inbox_test";
        public const string OutboxQueue = "outbox_test";

        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.

        public class Fixture : IAsyncLifetime
        {           
            private PostgreSqlContainer PostgreSqlContainer { get; set; } = null!;

            private KafkaContainer KafkaContainer { get; set; } = null!;

            public ServiceProvider ServiceProvider { get; private set; } = null!;

            public ExecuteStepByStepGroupMiddleware<Guid>.IHandler StubHander { get; private set; } = null!;

            public async Task InitializeAsync()
            {
                TestcontainersSettings.WaitStrategyRetries = 1;
                TestcontainersSettings.WaitStrategyInterval = TimeSpan.FromSeconds(1);
                TestcontainersSettings.WaitStrategyTimeout = TimeSpan.FromSeconds(4);

                {
                    var postgresBuilder = new PostgreSqlBuilder("postgres:18")
                        .WithPortBinding(15433, PostgreSqlBuilder.PostgreSqlPort);
                    PostgreSqlContainer = postgresBuilder.Build();

                    var kafkaBuilder = new KafkaBuilder("apache/kafka-native:4.0.2");
                    KafkaContainer = kafkaBuilder.Build();
                }
                
                var startTasks = new Task[] 
                {
                    PostgreSqlContainer.StartAsync(),
                    KafkaContainer.StartAsync()
                };
                await Task.WhenAll(startTasks);

                ServiceProvider = ConfigureServices();

                try
                {
                    await using (var scope = ServiceProvider.CreateAsyncScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                        await dbContext.Database.EnsureCreatedAsync();
                    }
                }
                catch
                {
                    await DisposeAsync();
                    throw;
                }
            }

            private ServiceProvider ConfigureServices()
            {
                var services = new ServiceCollection();

                services

                    .AddDbServices(
                        (s) => new TestDbContext(
                        s,
                        $"Host=localhost;Port={PostgreSqlContainer.GetMappedPublicPort()};Database=test;Username=postgres;Password=postgres;Include Error Detail=True;"),
                        typeof(EFWakeupDbProvider<Guid>)
                        )

                    .AddKafkaServices(
                        new KafkaQueueProviderFactory.OptionsDto(
                            $"localhost:{KafkaContainer.GetMappedPublicPort()}",
                            10,
                            (_) => "test",
                            (_) => 1
                            )
                    )
                    .AddIsolationServices()

                    .AddProcessExecutionServices(
                        new LocalProcessBufferService<Guid>.Options() { SizeLimit = 1 },
                        processCountLimiter: 1
                    )

                    .AddWakeupServices(
                        new WakeupRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(10, 1), 1), typeof(EFInboxMessageWakeupHandler<Guid>)),
                        new WakeupRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(11, 1), 1), typeof(EFOutboxMessageWakeupHandler<Guid>))
                    )

                    .AddTriggerServices(
                        new TriggerRegistryDto(WakeupTriggerRangeHandler<Guid>.Name, typeof(WakeupTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(NoWakeupRetryTriggerRangeHandler<Guid>.Name, typeof(NoWakeupRetryTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(WakeupStreamTriggerRangeHandler<Guid>.Name, typeof(WakeupStreamTriggerRangeHandler<Guid>))
                    )

                    .AddTriggerEngineServices(
                        new TriggerRunner<Guid>.Options() 
                        {
                            DbExecuteParallelismLimit = 1,
                            DbExecuteSelectLockTimeout = TimeSpan.FromSeconds(30),
                            DbExecuteWaitTriggerLockTimeout = TimeSpan.FromSeconds(30),
                            QueueConsumePackSize = 10,
                            QueueConsumeBatchTimeout = TimeSpan.FromSeconds(1),
                        },
                        new TriggerOptions() 
                        {
                            TriggerEventQueueName = TriggerQueue,
                        }
                        )

                    .AddProcessServices(
                        new EFChangeTrackerProcessRepository<Guid, ProcessDbEntity<Guid>>.Options() 
                        {
                            RetryLimit = 2,
                            SoftTimeout = null,
                        },
                        new ProcessRegistryDto(new ProcessTypeDto(10, 1), 1),
                        new ProcessRegistryDto(new ProcessTypeDto(11, 1), 1)
                    );

                // StubHander = Substitute.For<ExecuteStepByStepGroupMiddleware<Guid>.IHandler>();
                services.AddScoped<IProcessRunner>(
                    s => new ProcessRunner<Guid>(
                        s,
                        new ProcessRunner<Guid>.OptionsDto(
                            SelectBatchLimit: 1,
                            selectEmptyTimeout: TimeSpan.FromSeconds(1),
                            BatchLimit: 1,
                            BatchTimeout: TimeSpan.FromSeconds(1),
                            SelectFactory: (s) => s.GetRequiredService<EFProcessSelectQuery<Guid, ProcessDbEntity<Guid>>>(),                        
                            RootMiddlewareFactory: (s) => new TransactionMiddleware<Guid>(
                            s,
                            (s, ids) =>
                            {
                                var inbox = s.GetRequiredService<InboxRegistryDto>();
                                var outbox = s.GetRequiredService<OutboxRegistryDto>();

                                if (ids.First().First().ProcessType == inbox.Registry.ProcessType)
                                {
                                    return new ExecuteStepByStepGroupMiddleware<Guid>(
                                        s,
                                        s.GetRequiredService<IIsolationService>(),
                                        s.GetRequiredService<IProcessSetter>(),
                                        s.GetRequiredService<IWakeupService<Guid>>(),
                                        (s) => ValueTask.FromResult((ExecuteStepByStepGroupMiddleware<Guid>.IHandler)s.GetRequiredService<TestInboxBody>()),
                                        s.GetRequiredService<IProcessContainerConditions<Guid>>()
                                        );
                                }
                                else if (ids.First().First().ProcessType == outbox.Registry.ProcessType)
                                {
                                    return new ExecuteStepByStepGroupMiddleware<Guid>(
                                        s,
                                        s.GetRequiredService<IIsolationService>(),
                                        s.GetRequiredService<IProcessSetter>(),
                                        s.GetRequiredService<IWakeupService<Guid>>(),
                                        (s) => ValueTask.FromResult((ExecuteStepByStepGroupMiddleware<Guid>.IHandler)s.GetRequiredService<OutboxRangeProcessHandler<Guid>>()),
                                        s.GetRequiredService<IProcessContainerConditions<Guid>>()
                                        );
                                }
                                else
                                {
                                    throw new NotImplementedException("Test");
                                }
                            },
                            s.GetRequiredService<ITransactionManager>()
                            )),                    
                        s.GetRequiredService<ILocalProcessBufferService<Guid>>(),                    
                        s.GetRequiredService<IExecuteLimiterInvoker>(),
                        s.GetRequiredService<ProcessCountLimiter>()                        
                        )
                );
                services
                    .AddScoped<TestInboxBody>()
                    .AddSingleton(new BaseSingleProcessHandler<Guid>.OptionsDto(
                        new ExecuteStepByStepGroupMiddleware<Guid>.OptionsDto(
                            10,
                            IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker,
                            true,
                            false,
                            true),
                        IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker,
                        UseSave: true));

                services
                    .AddInboxOutbox(
                        new InboxRunner<Guid>.OptionsDto() 
                        {
                            ConsumeBatchSize = 100,
                            ConsumeTimeout = TimeSpan.FromSeconds(2),
                            Queues = [InboxQueue],
                        },
                        new EFInboxConsumerService<Guid>.Options() 
                        {
                            IdempotencyIdFactory = (m) => m.Key,
                            AggregateIdFactory = (m) => new AggregateDto("0", "0")
                        },
                        new EFInboxDbProvider<Guid>.Options()
                        { 
                            MessageLimitFunc = (m) => m * 10,
                        },
                        new EFOutboxDbProvider<Guid>.Options() 
                        {
                            MessageLimitFunc = (m) => m * 10,
                        },
                        new InboxRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(10, 1), 1)),
                        new OutboxRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(11, 1), 1))
                    );

                services
                    .AddScoped<BuisnessEntityForInboxDbProvider>()
                    .AddScoped<IProcessDbProvider<Guid>>(s => s.GetRequiredService<BuisnessEntityForInboxDbProvider>());

                return services.BuildServiceProvider(
                    new ServiceProviderOptions()
                    { 
                        ValidateOnBuild = true,
                        ValidateScopes = true
                    });
            }

            public async Task CleanEnvironmentAsync() 
            {
                await using (var scope = ServiceProvider.CreateAsyncScope())
                {
                    // InMemory
                    {
                        var cachce = scope.ServiceProvider.GetRequiredService<EFClassifierRepository<Guid>.CachState>();
                        cachce._queueCache.Clear();
                        cachce._aggregateCache.Clear();
                        cachce._inboxInfo.Clear();
                        cachce._outboxInfo.Clear();
                        cachce._inboxOffset.Clear();
                        cachce._outboxOffset.Clear();
                    }

                    // Db
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                        await dbContext.TruncateAllAsync();
                    }                   

                    // Kafka
                    {
                        var connectionString = $"localhost:{KafkaContainer.GetMappedPublicPort("9092")}";
                        using var client = new AdminClientBuilder(
                            new AdminClientConfig()
                            {
                                BootstrapServers = connectionString
                            }
                            )
                            .Build();

                        var metadata = client.GetMetadata(TimeSpan.FromSeconds(5));
                        var topics = metadata.Topics
                            .Select(e => e.Topic)
                            .Where(e => !e.StartsWith("__")) // Не трогаем системные топики
                            .ToArray();

                        if (topics.Length != 0)
                        {
                            await client.DeleteTopicsAsync(
                                topics
                                );
                        }
                    }
                }
            }

            public async Task DisposeAsync()
            {
                await ServiceProvider.DisposeAsync();

                await Task.WhenAll(
                    [
                    PostgreSqlContainer?.DisposeAsync().AsTask() ?? Task.CompletedTask,
                    KafkaContainer.DisposeAsync().AsTask() ?? Task.CompletedTask
                    ]
                    );
            }
        }
    }
}
