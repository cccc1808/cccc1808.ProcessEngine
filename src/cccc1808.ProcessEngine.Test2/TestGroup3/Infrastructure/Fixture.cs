using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Retry;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Redis.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Queue;
using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Reserve;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;

using Confluent.Kafka;

using Microsoft.Extensions.DependencyInjection;

using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

using Xunit.Sdk;

namespace cccc1808.ProcessEngine.Test2.TestGroup3.Infrastructure
{
    [CollectionDefinition(Name)]
    public class FixtureCollection : ICollectionFixture<FixtureCollection.Fixture>
    {       
        public const string Name = "FixtureCollection 3";
        public const int RangeConst = 1000;

        private static string RedisConnectionName { get; } = "1";

        private static int RedisDb { get; } = -1;

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

                        await dbContext.OptimizeConfigurationAsync();
                    }

                    await PostgreSqlContainer.StopAsync();
                    await PostgreSqlContainer.StartAsync();
                    await Task.Delay(TimeSpan.FromSeconds(1));
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
                        typeof(ChildProcessDbProvider)
                        )
                    .AddKafkaServices(
                        new KafkaQueueProviderFactory.OptionsDto(
                            $"localhost:{KafkaContainer.GetMappedPublicPort()}",
                            producerBatchSize: 250,
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

                    // StubHander = Substitute.For<ExecuteStepByStepGroupMiddleware<Guid>.IHandler>();
                    .AddQueueProcessRunner(
                        new QueueProcessRunner<Guid>.OptionsDto() 
                        {
                            DbSelect_Options = new EFQueueProcessRunnerQuery<Guid>.Options() 
                            {
                                BatchSize = 1,
                                OffsetStartId = Guid.Empty,
                            },
                            DbSelect_ParallilLimit = 1,
                            DbSelect_EmptyDelay = TimeSpan.FromSeconds(2),

                            RangeExecute_MiddlewareFactory = (s) => throw new Exception(""),

                            SingleExecute_ParallelismLimit = 1,
                            SingleExecute_MiddlewareFactory = (s) => new TransactionMiddleware<Guid>(
                                s,
                                (s, _) => new ExecuteStepByStepGroupMiddleware<Guid>(
                                    s,
                                    s.GetRequiredService<IDateTimeProvider>(),
                                    s.GetRequiredService<IIsolationService>(),
                                    s.GetRequiredService<IProcessSetter>(),
                                    s.GetRequiredService<IProcessQueueContext<Guid>>(),
                                    s.GetRequiredService<ITriggerEventRaiser<Guid>>(),
                                    (s) => ValueTask.FromResult((ExecuteStepByStepGroupMiddleware<Guid>.IHandler)s.GetRequiredService<TestProcessBody>()),
                                    s.GetRequiredService<IProcessContainerConditions<Guid>>()
                                    ),
                                s.GetRequiredService<ITransactionManager>()
                                ),

                            ExceptionDelay = TimeSpan.Zero,
                        })
                    .AddTriggerServices(
                        new TriggerRegistryDto(NoWakeupRetryTriggerRangeHandler<Guid>.Name, typeof(NoWakeupRetryTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(ParentProcessTriggerHandler.Name, typeof(ParentProcessTriggerHandler))
                    )
                    .AddTriggerEngineServices(
                        new TriggerRunner<Guid>.OptionsDto()
                        {
                            DbSelect_ParallilLimit = 1,
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
                                    QueueName = "trigger_events",
                                    QueueConsumeMessagesLimit = FixtureCollection.RangeConst,
                                    QueueConsumeBatchTimeout = TimeSpan.FromSeconds(3),
                                }
                            }                            
                        },
                        new TriggerOptions<Guid>() 
                        {
                            PartitionSelector = (e) => e.ProcessId.GetHashCode() % 1
                        },
                        new RedisTriggerQueueOptionsDto<Guid>()
                        {
                            ConnectionName = FixtureCollection.RedisConnectionName,
                            DbId = FixtureCollection.RedisDb,
                            IdToString = NameFactory.IdToString,
                            StringToId = NameFactory.StringToId,
                            HandlerToQueueSetNameFactory = (e) => NameFactory.TriggerTypeToKey(e, NameFactory.TriggerQueue),
                            QueueSetNameToHandlerFactory = (e) => NameFactory.KeyToTriggerType(e),
                            QueueChannelNameFactory = (e) => NameFactory.TriggerTypeToKey(e, NameFactory.TriggerQueueChannel),
                        },
                        new RedisTriggerReserveProvider<Guid>.OptionsDto()
                        {
                            ConnectionName = FixtureCollection.RedisConnectionName,
                            DbId = FixtureCollection.RedisDb,
                            HashKey = NameFactory.TriggerReserve,
                            KeyToStringHandler = NameFactory.IdToString,
                            StringToKeyHandler = NameFactory.StringToId,
                        }
                        )
                    .AddProcessServices(
                        new EFChangeTrackerProcessRepository<Guid, ProcessDbEntity<Guid>>.Options()
                        {
                            RetryLimit = 2,
                            SoftTimeout = null,
                        },
                        new ProcessRegistryDto(new ProcessTypeUniqueDto(new ProcessTypeDto(1, 1), 1), new ProcessTypeMetadata(IsSignleExecuteProcess: true)),
                        new ProcessRegistryDto(new ProcessTypeUniqueDto(new ProcessTypeDto(2, 1), 1), new ProcessTypeMetadata(IsSignleExecuteProcess: true)),
                        new ProcessRegistryDto(new ProcessTypeUniqueDto(new ProcessTypeDto(3, 1), 1), new ProcessTypeMetadata(IsSignleExecuteProcess: true)),
                        new ProcessRegistryDto(new ProcessTypeUniqueDto(new ProcessTypeDto(4, 1), 1), new ProcessTypeMetadata(IsSignleExecuteProcess: true))
                    );                

                services
                    .AddScoped<TestProcessBody>();                

                return services.BuildServiceProvider();
            }

            public async Task CleanEnvironmentAsync() 
            {
                await using (var scope = ServiceProvider.CreateAsyncScope())
                {
                    // Db
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                        await dbContext.TruncateAllAsync();

                        // TODO: заменить на truncate table.
                        //await dbContext.Database.EnsureDeletedAsync();
                        //await dbContext.Database.EnsureCreatedAsync();
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
