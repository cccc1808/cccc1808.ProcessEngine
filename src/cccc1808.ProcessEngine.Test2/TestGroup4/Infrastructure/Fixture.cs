using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
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
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Retry;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Stream;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.ClassifierModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Wakeup;
using cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.InboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.OutboxModule.Services;
using cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.T2;
using cccc1808.ProcessEngine.Model.Redis.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.T2;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services;

using Confluent.Kafka;

using Microsoft.Extensions.DependencyInjection;

using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

using Xunit.Sdk;

namespace cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class FixtureCollection : ICollectionFixture<FixtureCollection.Fixture>
    {
        public const string Name = "FixtureCollection 4";
        public const int TestTimeout = 10000;

        private static string RedisConnectionName { get; } = "1";
        private static int RedisDb { get; } = -1;

        public const string TriggerQueue = "trigger_events";
        public const string InboxQueue = "inbox_test";
        public const string OutboxQueue = "outbox_test";

        public class Fixture : IAsyncLifetime
        {           
            private PostgreSqlContainer PostgreSqlContainer { get; set; } = null!;

            private KafkaContainer KafkaContainer { get; set; } = null!;

            public ServiceProvider ServiceProvider { get; private set; } = null!;

            private RedisContainer RedisContainer { get; set; } = null!;

            public ExecuteStepByStepGroupMiddleware<Guid>.IHandler StubHander { get; private set; } = null!;

            public async Task InitializeAsync()
            {
                {
                    var postgresBuilder = new PostgreSqlBuilder("postgres:18")
                        .WithPortBinding(15433, PostgreSqlBuilder.PostgreSqlPort);
                    PostgreSqlContainer = postgresBuilder.Build();

                    var kafkaBuilder = new KafkaBuilder("apache/kafka-native:4.0.2");
                    KafkaContainer = kafkaBuilder.Build();

                    var redisBuilder = new RedisBuilder("redis:7.4");
                    RedisContainer = redisBuilder.Build();
                }
                
                var startTasks = new Task[] 
                {
                    PostgreSqlContainer.StartAsync(),
                    KafkaContainer.StartAsync(),
                    RedisContainer.StartAsync(),
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
                    .AddTestService()

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
                    .AddRedis(
                        new RedisConnectionFactory.OptionsDto()
                        {
                            ConnectionConfigrations = new Dictionary<string, (string ConnectionString, TimeSpan PiplineTimeout)>()
                            {
                                [FixtureCollection.RedisConnectionName] = new($"localhost:{RedisContainer.GetMappedPublicPort()}", TimeSpan.FromSeconds(10))
                            }
                        }
                    )
                    .AddIsolationServices()

                    .AddParallelLimitProcessRunner()
                    .AddEFProcessReservationService()

                    .AddWakeupServices(
                        [
                            new WakeupRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(11, 1), 1), WakeupStateEnum.CheckWakeupWithoutLock, typeof(EFOutboxMessageWakeupHandler<Guid>))
                        ],
                        [
                            new StreamRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(10, 1), 1)),
                            new StreamRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(11, 1), 1)),
                            ]
                    )

                    .AddTriggerServices(
                        new TriggerRegistryDto(WakeupTriggerRangeHandler<Guid>.Name, typeof(WakeupTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(NoWakeupRetryTriggerRangeHandler<Guid>.Name, typeof(NoWakeupRetryTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(WakeupStreamTriggerRangeHandler<Guid>.Name, typeof(WakeupStreamTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(NoWakeupStreamTriggerRangeHandler<Guid>.Name, typeof(NoWakeupStreamTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(EFOutboxTriggerWakeupHandler<Guid>.Name, typeof(EFOutboxTriggerWakeupHandler<Guid>))
                    )

                    .AddTriggerEngineServices(
                        new TriggerRunner<Guid>.OptionsDto() 
                        {
                            DbSelect_Options = new EFTriggerSelectQuery<Guid>.OptionsDto()
                            {
                                BatchSize = 1,
                                StartOffset = Guid.Empty,
                            },

                            RangeExecutor_ExecuteParallelismLimit = 1,
                            SingleExecutor_ParallelismLimit = 1,

                            Consumer_TriggerEventQueues = new List<TriggerRunner<Guid>.QueueOptionsDto>() 
                            {
                                new TriggerRunner<Guid>.QueueOptionsDto()
                                {
                                    QueueName = TriggerQueue,
                                    QueueConsumeMessagesLimit = 10,
                                    QueueConsumeBatchTimeout = TimeSpan.FromSeconds(0.5),
                                }
                            },                            
                        },
                        new TriggerOptions<Guid>() 
                        {
                            PartitionSelector = (e) => e.ProcessId.GetHashCode() % 1
                        },
                        new RedisTriggerQueueOptionsDto<Guid>()
                        {
                            ConnectionName = FixtureCollection.RedisConnectionName,
                            DbId = FixtureCollection.RedisDb,
                            IdToString = (e) => e.ToString(),
                            StringToId = (e) => Guid.Parse(e),
                            HandlerToQueueSetNameFactory = (e) => $"trigger_queue{NameConst.NamePartsSplitChar}{e.HandlerName}{NameConst.NamePartsSplitChar}{e.Priority}",
                            QueueSetNameToHandlerFactory = (e) =>
                            {
                                var parts = e.Split(NameConst.NamePartsSplitChar);
                                return new IRedisNotifyTriggerQueueState.KeyDto(parts[1], short.Parse(parts[2]));
                            },
                            QueueChannelNameFactory = (e) => $"trigger_queue_channel{NameConst.NamePartsSplitChar}{e.HandlerName}{NameConst.NamePartsSplitChar}{e.Priority}",
                        },
                        new RedisTriggerReservationOptions()
                        {
                            ConnectionName = FixtureCollection.RedisConnectionName,
                            DbId = FixtureCollection.RedisDb,
                        },
                        new RedisTriggerReservationProvider<Guid>.OptionsDto()
                        {
                            KeyToStringHandler = (e) => e.ToString(),
                            StringToKeyHandler = (e) => Guid.Parse(e),
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

                services.AddScoped<IProcessRunner>(
                    s => new ParallelLimitProcessRunner<Guid>(
                        s,
                        new ParallelLimitProcessRunner<Guid>.OptionsDto(
                            selectOptions: new EFParallelLimitProcessSelectQuery<Guid, ProcessDbEntity<Guid>>.Options1()
                            {
                                RangeBatchSize = (e) => e,
                                SingleBatchSize = (e) => e,
                            },
                            selectFactory: (s) => s.GetRequiredService<EFParallelLimitProcessSelectQuery<Guid, ProcessDbEntity<Guid>>>(),
                            rangeMiddlewareFactory: (s) => throw new Exception(""),
                            signleMiddlewareFactory: (s) => new TransactionMiddleware<Guid>(
                            s,
                            (s, ids) =>
                            {
                                var inbox = s.GetRequiredService<InboxRegistryDto>();
                                var outbox = s.GetRequiredService<OutboxRegistryDto>();

                                if (ids.First().First().ProcessType == inbox.Registry.ProcessType)
                                {
                                    return new ExecuteStepByStepGroupMiddleware<Guid>(
                                        s,
                                        s.GetRequiredService<IDateTimeProvider>(),
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
                                        s.GetRequiredService<IDateTimeProvider>(),
                                        s.GetRequiredService<IIsolationService>(),
                                        s.GetRequiredService<IProcessSetter>(),
                                        s.GetRequiredService<IWakeupService<Guid>>(),
                                        (s) => ValueTask.FromResult((ExecuteStepByStepGroupMiddleware<Guid>.IHandler)s.GetRequiredService<OutboxRangeProcessHandler1<Guid>>()),
                                        s.GetRequiredService<IProcessContainerConditions<Guid>>()
                                        );
                                }
                                else
                                {
                                    throw new NotImplementedException("Test");
                                }
                            },
                            s.GetRequiredService<ITransactionManager>()
                            )                       
                            )
                        {
                            ExceptionDelay = TimeSpan.Zero,
                            DbExecuteParallelismLimit = 1,
                        })
                );


                services
                    .AddScoped<TestInboxBody>()
                    .AddSingleton(new BaseSingleProcessHandler<Guid>.OptionsDto(
                        Presets<Guid>.Preset1,
                        IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker,
                        UseSave: true));

                services
                    .AddInboxOutbox(
                        new InboxRunner<Guid>.OptionsDto() 
                        {
                            ConsumeBatchLimit = 100,
                            ConsumeBatchTimeout = TimeSpan.FromMilliseconds(100),
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
                        new EFOutboxDbProvider1<Guid>.Options() 
                        {
                            MessageLimitFunc = (m) => m * 10,
                        },
                        new InboxRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(10, 1), 1), TriggerQueue),
                        new OutboxRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(11, 1), 1), TriggerQueue)
                    );

                services
                    .AddScoped<BuisnessEntityForInboxDbProvider>()
                    .AddScoped<IProcessDbProvider<Guid>>(s => s.GetRequiredService<BuisnessEntityForInboxDbProvider>());

                // Чтобы не падала ошибка DI.
                services.AddSingleton(new EmergencyTriggerHandler<Guid>.OptionsDto(
                    TriggerQueue
                    ));

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
                        cachce.Clear();
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
                    KafkaContainer?.DisposeAsync().AsTask() ?? Task.CompletedTask,
                    RedisContainer?.DisposeAsync().AsTask() ?? Task.CompletedTask,
                    ]
                    );
            }
        }
    }
}
