using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.ExternalCounter;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Retry;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Stream;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Storage.ExternalCounter;
using cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Redis.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services.RootTrigger;

using Confluent.Kafka;

using Microsoft.Extensions.DependencyInjection;

using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

using Xunit.Sdk;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class FixtureCollection : ICollectionFixture<FixtureCollection.Fixture>
    {       
        public const string Name = "FixtureCollection 2";
        public const int TestTimeout = 115000;

        public class Fixture : IAsyncLifetime
        {           
            private PostgreSqlContainer PostgreSqlContainer { get; set; } = null!;

            private KafkaContainer KafkaContainer { get; set; } = null!;

            private RedisContainer RedisContainer { get; set; } = null!;

            public ServiceProvider ServiceProvider { get; private set; } = null!;

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

                var tryStartCount = 0;
                while(true)
                {
                    var startTasks = new Task[]
                    {
                        PostgreSqlContainer.StartAsync(),
                        KafkaContainer.StartAsync(),
                        RedisContainer.StartAsync()
                    };
                    try 
                    {
                        await Task.WhenAll(startTasks);
                        break;
                    }
                    catch(Docker.DotNet.DockerApiException)
                    {
                        if (tryStartCount > 2)
                        {
                            throw;
                        }
                        tryStartCount++;
                    }
                }                

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
                        typeof(EFWakeupDbProvider<Guid>),
                        typeof(ChildProcessDbProvider),
                        typeof(RootTriggerDbProvider)
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
                                ["1"] = new ($"localhost:{RedisContainer.GetMappedPublicPort()}", TimeSpan.FromSeconds(10))
                            }
                        }
                    )
                    .AddRedisExternalCounter(
                        new RedisExternalCounterProvider.OptionsDto() 
                        {
                            ConnectionName = "1",
                            DatabaseId = -1,
                        }
                    )

                    .AddIsolationServices()

                    .AddParallelLimitProcessRunner()

                    .AddWakeupServices(
                        [new WakeupRegistryDto(new ProcessRegistryDto(new ProcessTypeDto(3, 1), 1), WakeupStateEnum.CheckWakeupWithLock, typeof(ParentCheckWakeupHandler))],
                        []
                    )

                    .AddTriggerServices(
                        new TriggerRegistryDto(WakeupTriggerRangeHandler<Guid>.Name, typeof(WakeupTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(NoWakeupRetryTriggerRangeHandler<Guid>.Name, typeof(NoWakeupRetryTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(ParentProcessTriggerHandler.Name, typeof(ParentProcessTriggerHandler)),
                        new TriggerRegistryDto(EmergencyTriggerHandler<Guid>.Name, typeof(EmergencyTriggerHandler<Guid>)),
                        new TriggerRegistryDto(NoWakeupStreamTriggerRangeHandler<Guid>.Name, typeof(NoWakeupStreamTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(Services.RootTrigger.ChildTriggerHandler.Name, typeof(Services.RootTrigger.ChildTriggerHandler))
                    )
                    .AddSingleton(
                        new EmergencyTriggerHandler<Guid>.OptionsDto(
                            "trigger_events"
                            )
                        {
                            BatchSize = 1,
                            SoftTimeout = TimeSpan.FromMinutes(1),
                            LostTriggerTimeout = TimeSpan.Zero,
                        }
                        )

                    .AddTriggerEngineServices(
                        new TriggerRunner<Guid>.OptionsDto(
                            new EFTriggerSelectQuery<Guid>.Options3()
                            {
                                SingleTriggerBatchSize = (_) => 1,
                            }
                            ) 
                        {
                            DbExecuteParallelismLimit = 1,
                            DbExecuteSelectLockTimeout = TimeSpan.FromSeconds(30),
                            DbExecuteWaitTriggerLockTimeout = TimeSpan.FromSeconds(30),
                            TriggerEventQueues = new List<TriggerRunner<Guid>.QueueOptionsDto>()
                            {
                                new TriggerRunner<Guid>.QueueOptionsDto()
                                {
                                    QueueName = "trigger_events",
                                    QueueConsumeMessagesLimit = 10,
                                    QueueConsumeBatchTimeout = TimeSpan.FromSeconds(1),
                                }
                            }                            
                        },
                        new TriggerOptions<Guid>() 
                        {
                            PartitionSelector = (e) => e.ProcessId.GetHashCode() % 1
                        }
                        )

                    .AddProcessServices(
                        new EFChangeTrackerProcessRepository<Guid, ProcessDbEntity<Guid>>.Options() 
                        {
                            RetryLimit = 2,
                            SoftTimeout = null,
                        },
                        new ProcessRegistryDto(new ProcessTypeDto(1, 1), 1),
                        new ProcessRegistryDto(new ProcessTypeDto(2, 1), 1),
                        new ProcessRegistryDto(new ProcessTypeDto(3, 1), 1),
                        new ProcessRegistryDto(new ProcessTypeDto(4, 1), 1),
                        new ProcessRegistryDto(new ProcessTypeDto(5, 1), 1)
                    );

                // StubHander = Substitute.For<ExecuteStepByStepGroupMiddleware<Guid>.IHandler>();
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
                                (s, _) => new ExecuteStepByStepGroupMiddleware<Guid>(
                                    s,
                                    s.GetRequiredService<IDateTimeProvider>(),
                                    s.GetRequiredService<IIsolationService>(),
                                    s.GetRequiredService<IProcessSetter>(),
                                    s.GetRequiredService<IWakeupService<Guid>>(),
                                    (s) => ValueTask.FromResult((ExecuteStepByStepGroupMiddleware<Guid>.IHandler)s.GetRequiredService<TestProcessBody>()),
                                    s.GetRequiredService<IProcessContainerConditions<Guid>>()
                                    ),
                                s.GetRequiredService<ITransactionManager>()
                                )
                        )
                        { 
                            ExceptionDelay = TimeSpan.Zero,
                            DbExecuteParallelismLimit = 1,
                        })
                );
                services
                    .AddScoped<TestProcessBody>()
                    .AddSingleton<TestProcessBody.TestState>();                

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

                    {
                        var externalCounter = scope.ServiceProvider.GetRequiredService<IExternalCounterProvider>();
                        await externalCounter.ClearAsync();
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
