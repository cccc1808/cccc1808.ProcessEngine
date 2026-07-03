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
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Retry;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Stream;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Storage.DbProviders;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.Infrastructure.ParentChildModule.Dto;
using cccc1808.ProcessEngine.Test2.Infrastructure.ParentChildModule.Storage;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process1;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process2;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process3;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process4;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process5;

using Confluent.Kafka;

using Microsoft.Extensions.DependencyInjection;

using Testcontainers.Kafka;
using Testcontainers.PostgreSql;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class FixtureCollection : ICollectionFixture<FixtureCollection.Fixture>
    {
        public const string Name = "FixtureCollection 5";
        public const int TestTimeout = 115000;

        public static string TriggerEvents 
            => "trigger_events";

        public class Fixture : IAsyncLifetime
        {
            private PostgreSqlContainer PostgreSqlContainer { get; set; } = null!;

            private KafkaContainer KafkaContainer { get; set; } = null!;

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
                }

                var tryStartCount = 0;
                while (true)
                {
                    var startTasks = new Task[]
                    {
                        PostgreSqlContainer.StartAsync(),
                        KafkaContainer.StartAsync()
                    };
                    try
                    {
                        await Task.WhenAll(startTasks);
                        break;
                    }
                    catch (Docker.DotNet.DockerApiException)
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
                        typeof(EFSchemaProcessDataDbEntityDbProvider<Guid>),
                        typeof(ParentChildDbProvider)
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

                    .AddParallelLimitProcessRunner()

                    .AddWakeupServices(
                        [],
                        []
                    )

                    .AddTriggerServices(
                        new TriggerRegistryDto(WakeupTriggerRangeHandler<Guid>.Name, typeof(WakeupTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(NoWakeupRetryTriggerRangeHandler<Guid>.Name, typeof(NoWakeupRetryTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(EmergencyTriggerHandler<Guid>.Name, typeof(EmergencyTriggerHandler<Guid>)),
                        new TriggerRegistryDto(NoWakeupStreamTriggerRangeHandler<Guid>.Name, typeof(NoWakeupStreamTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers.EFTimerChildTriggerHandler<Guid>.Name, typeof(cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers.EFTimerChildTriggerHandler<Guid>))
                    )
                    .AddSingleton(
                        new EmergencyTriggerHandler<Guid>.OptionsDto(
                            FixtureCollection.TriggerEvents
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
                                    QueueName = TriggerEvents,
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
                            SoftTimeout = TimeSpan.FromSeconds(60),
                        },
                        new ProcessRegistryDto(TestSchemaProcessHandler.ProcessType, 1),
                        new ProcessRegistryDto(TestSchemaProcessHandler2.ProcessType, 1),
                        new ProcessRegistryDto(TestSchemaProcessHandler51.ProcessType, 1),
                        new ProcessRegistryDto(TestSchemaProcessHandler52.ProcessType, 1)
                    )
                    
                    .AddSchemaProcess(
                        SchemaProcessRegistrationDto.Create<Guid, TestSchemaProcessHandler, SchemaProcessStateTypelessHandler<Guid>>(TestSchemaProcessHandler.ProcessType, useSignalCode: false),
                        SchemaProcessRegistrationDto.Create<Guid, TestSchemaProcessHandler2, SchemaProcessStateTypelessHandler<Guid>>(TestSchemaProcessHandler2.ProcessType, useSignalCode: false),
                        SchemaProcessRegistrationDto.Create<Guid, TestSchemaProcessHandler4, SchemaProcessStateTypelessHandler<Guid>>(TestSchemaProcessHandler4.ProcessType, useSignalCode: false),
                        SchemaProcessRegistrationDto.Create<Guid, TestSchemaProcessHandler51, SchemaProcessStateTypelessHandler<Guid>>(TestSchemaProcessHandler51.ProcessType, useSignalCode: false),
                        SchemaProcessRegistrationDto.Create<Guid, TestSchemaProcessHandler52, SchemaProcessStateTypelessHandler<Guid>>(TestSchemaProcessHandler52.ProcessType, useSignalCode: false)
                        );

                services
                    .AddScoped<ExternalHandlers2>()
                    .AddScoped<ExternalHandlers3>()
                    .AddScoped<ExternalHandlers4>();

                services.AddSingleton(
                    new ChildRegistrationDto(TestSchemaProcessHandler52.ProcessType));

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
                                    (s) => ValueTask.FromResult((ExecuteStepByStepGroupMiddleware<Guid>.IHandler)s.GetRequiredService<SchemaSingleProcessHandler<Guid>>()),
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
