using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Services;
using cccc1808.ProcessEngine.Test2.Infrastructure;

using Confluent.Kafka;

using DotNet.Testcontainers.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Testcontainers.Kafka;
using Testcontainers.PostgreSql;

using Xunit.Sdk;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure
{
    [CollectionDefinition(Name)]
    public class FixtureCollection : ICollectionFixture<FixtureCollection.Fixture>
    {       
        public const string Name = "FixtureCollection 2";

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
                    var postgresBuilder = new PostgreSqlBuilder(
                        "postgres:18"
                        //"allansimon/docker-postgres-for-testing:patch-1"
                        )
                        .WithPortBinding(15433, PostgreSqlBuilder.PostgreSqlPort);
                    PostgreSqlContainer = postgresBuilder.Build();

                    var kafkaBuilder = new KafkaBuilder("apache/kafka-native:4.0.2");
                    KafkaContainer = kafkaBuilder.Build();
                }

                ServiceProvider = ConfigureServices();

                // wait for TCP Port 5432 with a timeout of 10s
                // WaitForPort.Ports.WaitForTcpPort(15433, (int)TimeSpan.FromSeconds(5).TotalMilliseconds);

                var startTasks = new Task[] 
                {
                    PostgreSqlContainer.StartAsync(),
                    KafkaContainer.StartAsync()
                };
                await Task.WhenAll(startTasks);

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
                        $"Host=localhost;Port={PostgreSqlContainer.GetMappedPublicPort()};Database=test;Username=postgres;Password=postgres;Include Error Detail=True;"))
                    .AddIsolationServices()
                    .AddProcessExecutionServices(
                        new LocalProcessBufferService<Guid>.Options() { SizeLimit = 1 },
                        processCountLimiter: 1
                    )
                    .AddWakeupServices()
                    .AddTriggerServices(
                        new TriggerRegistryDto(WakeupTriggerRangeHandler<Guid>.Name, typeof(WakeupTriggerRangeHandler<Guid>)),
                        new TriggerRegistryDto(NoWakeupRetryTriggerRangeHandler<Guid>.Name, typeof(NoWakeupRetryTriggerRangeHandler<Guid>))
                    )
                    .AddTriggerEngineServices(
                        new Model.Implementation.TriggerModule.Services.TriggerService<Guid>.Options() 
                        {
                            DbExecuteParallelismLimit = 1,
                            DbExecuteSelectLockTimeout = TimeSpan.FromSeconds(30),
                            DbExecuteWaitTriggerLockTimeout = TimeSpan.FromSeconds(30),
                        }
                        )
                    .AddProcessServices(
                        new ProcessRegistryDto(new ProcessTypeDto(1, 1), 1),
                        new ProcessRegistryDto(new ProcessTypeDto(2, 1), 1)
                    );

                // StubHander = Substitute.For<ExecuteStepByStepGroupMiddleware<Guid>.IHandler>();
                services.AddScoped<IProcessRunner>(
                    s => new ProcessRunner<Guid>(
                        s,
                        new ProcessRunner<Guid>.OptionsDto(
                            SelectBatchLimit: 1,
                            selectEmptyTimeout: TimeSpan.FromSeconds(1),
                            BatchLimit: 1,
                            BatchTimeout: TimeSpan.FromSeconds(1)),                    
                        s.GetRequiredService<ILocalProcessBufferService<Guid>>(),                    
                        s.GetRequiredService<IExecuteLimiterInvoker>(),
                        s.GetRequiredService<ProcessCountLimiter>(),
                        (s) => s.GetRequiredService<EFProcessSelectQuery<Guid, ProcessDbEntity<Guid>>>(),
                        (s) => new TransactionMiddleware<Guid>(
                            s,
                            (s) => new ExecuteStepByStepGroupMiddleware<Guid>(
                            s,
                            s.GetRequiredService<IIsolationService>(),
                            s.GetRequiredService<IProcessSetter>(),
                            s.GetRequiredService<IWakeupService<Guid>>(),
                            (s) => ValueTask.FromResult((ExecuteStepByStepGroupMiddleware<Guid>.IHandler)s.GetRequiredService<Process1Body>()),
                            s.GetRequiredService<IProcessContainerConditions<Guid>>()
                            ),
                            s.GetRequiredService<ITransactionManager>()
                            ) 
                        )
                );
                services
                    .AddScoped<Process1Body>();                

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
                    KafkaContainer.DisposeAsync().AsTask() ?? Task.CompletedTask
                    ]
                    );
            }
        }
    }
}
