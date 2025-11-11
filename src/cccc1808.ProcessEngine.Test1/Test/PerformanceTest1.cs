using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Services;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Groupping;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Parallel;
using cccc1808.ProcessEngine.Model.Implementation.Runners;
using cccc1808.ProcessEngine.Model.Implementation.Storage;
using cccc1808.ProcessEngine.Test1.Model;
using cccc1808.ProcessEngine.Test1.Model.Process1;
using cccc1808.ProcessEngine.Test1.Model.Process1.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

namespace cccc1808.ProcessEngine.Test1.Test
{
    [TestClass]
    public class PerformanceTest1
    {
        // private int TotalCount => 50000;


        [TestMethod]
        public async Task TestAsync() 
        {
            var count = 5000;
            var limit1 = 1000;
            var limit2 = 2000;

            var result = new Dictionary<string, Stopwatch>();

            if (true)
            {
                result.Add("1. useDbOptimizations: false, count, 50, limit1", await Test1Async(false, useDbOptimizations: false, count, 50, limit1));
                result.Add("1. useDbOptimizations: false, count, 100, limit1", await Test1Async(false, useDbOptimizations: false, count, 100, limit1));
                result.Add("1. useDbOptimizations: false, count, 250, limit1", await Test1Async(false, useDbOptimizations: false, count, 250, limit1));
                result.Add("1. useDbOptimizations: false, count, 500, limit1", await Test1Async(false, useDbOptimizations: false, count, 500, limit1));
            }

            //result.Add("1. useDbOptimizations: true, count, 50, limit1", await Test1Async(false, useDbOptimizations: true, count, 50, limit1));
            result.Add("1. useDbOptimizations: true, count, 100, limit1", await Test1Async(false, useDbOptimizations: true, count, 100, limit1));
            //result.Add("1. useDbOptimizations: true, count, 250, limit1", await Test1Async(false, useDbOptimizations: true, count, 250, limit1));
            //result.Add("1. useDbOptimizations: true, count, 500, limit1", await Test1Async(false, useDbOptimizations: true, count, 500, limit1));
            //result.Add("1. useDbOptimizations: true, count, 1000, limit1", await Test1Async(false, useDbOptimizations: true, count, 1000, limit1));

            //result.Add("1. useDbOptimizations: false, count, 50, limit2", await Test1Async(false, useDbOptimizations: false, count, 50, limit2));
            //result.Add("1. useDbOptimizations: false, count, 100, limit2", await Test1Async(false, useDbOptimizations: false, count, 100, limit2));
            //result.Add("1. useDbOptimizations: false, count, 250, limit2", await Test1Async(false, useDbOptimizations: false, count, 250, limit2));
            //result.Add("1. useDbOptimizations: false, count, 500, limit2", await Test1Async(false, useDbOptimizations: false, count, 500, limit2));

            //result.Add("1. useDbOptimizations: true, count, 50, limit2", await Test1Async(false, useDbOptimizations: true, count, 50, limit2));
            //result.Add("1. useDbOptimizations: true, count, 100, limit2", await Test1Async(false, useDbOptimizations: true, count, 100, limit2));
            //result.Add("1. useDbOptimizations: true, count, 250, limit2", await Test1Async(false, useDbOptimizations: true, count, 250, limit2));
            //result.Add("1. useDbOptimizations: true, count, 500, limit2", await Test1Async(false, useDbOptimizations: true, count, 500, limit2));
            //result.Add("1. useDbOptimizations: true, count, 1000, limit2", await Test1Async(false, useDbOptimizations: true, count, 1000, limit2));

            //var result1221 = await Test1Async(true, count, 50, limit2);
            //var result1222 = await Test1Async(true, count, 100, limit2);
            //var result1223 = await Test1Async(true, count, 250, limit2);
            //var result1224 = await Test1Async(true, count, 500, limit2);
            // var result125 = await Test1Async(true, count, 1000);

            result.Add("2. useDbOptimizations: false, count, Environment.ProcessorCount * 3", await Test2Async(false, useDbOptimizations: false, count, Environment.ProcessorCount * 3));
            result.Add("2. useDbOptimizations: false, count, Environment.ProcessorCount * 4", await Test2Async(false, useDbOptimizations: false, count, Environment.ProcessorCount * 4));
            result.Add("2. useDbOptimizations: false, count, Environment.ProcessorCount * 5", await Test2Async(false, useDbOptimizations: false, count, Environment.ProcessorCount * 5));

            //result.Add("2. useDbOptimizations: true, count, Environment.ProcessorCount * 3", await Test2Async(false, useDbOptimizations: true, count, Environment.ProcessorCount * 4));
            //result.Add("2. useDbOptimizations: true, count, Environment.ProcessorCount * 4", await Test2Async(false, useDbOptimizations: true, count, Environment.ProcessorCount * 5));
            //result.Add("2. useDbOptimizations: true, count, Environment.ProcessorCount * 5", await Test2Async(false, useDbOptimizations: true, count, Environment.ProcessorCount * 6));

            //var result211 = await Test2Async(false, count, Environment.ProcessorCount * 3);
            //var result212 = await Test2Async(false, count, Environment.ProcessorCount * 4);
            //var result213 = await Test2Async(false, count, Environment.ProcessorCount * 5);
            //var result214 = await Test2Async(false, count, Environment.ProcessorCount * 6);

            //var result221 = await Test2Async(true, 20000, Environment.ProcessorCount * 3);
            //var result222 = await Test2Async(true, 20000, Environment.ProcessorCount * 4);
            //var result223 = await Test2Async(true, 20000, Environment.ProcessorCount * 5);
            // var result224 = await Test2Async(true, 20000, Environment.ProcessorCount * 6);
        }


        public async Task<Stopwatch> Test1Async(
            bool useMemory,
            bool useDbOptimizations, 
            int total, 
            int batch,
            int limit) 
        {
            IServiceProvider serviceProvider;
            var waitComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var processed = 0;
            PostgreSqlContainer postgreSqlContainer;
            {
                var serviceCollection = new ServiceCollection();

                var postgresBuilder = new PostgreSqlBuilder()
                    .WithImage("postgres:18");

                postgreSqlContainer = postgresBuilder.Build();
                await postgreSqlContainer.StartAsync();

                await new DbInit().InitAllAsync(postgreSqlContainer, useDbOptimizations: useDbOptimizations);

                ModelRegistry.Registry(
                    serviceCollection,
                    new ProcessRunner<Guid>.OptionsDto(
                        SelectBatchLimit: 1000,
                        selectEmptyTimeout: TimeSpan.FromSeconds(4),
                        BatchLimit: batch,
                        BatchTimeout: TimeSpan.FromSeconds(2)),
                    ModelRegistry.Selector1(
                        TimeSpan.FromHours(1)),
                    (s) => new TestMiddleware(
                        s,
                        async (s, ids, t) =>
                        {
                            var middleware = new TransactionMiddleware<Guid>(
                                s,
                                (s) => new ExecuteStepByStepGroupMiddleware<Guid>(
                                    s.GetRequiredService<IServiceProvider>(),
                                    s.GetRequiredService<IIsolationService>(),
                                    s.GetRequiredService<IProcessSetter>(),
                                    s.GetRequiredService<IWakeUpService<Guid>>(),
                                    (s) => ValueTask.FromResult<ExecuteStepByStepGroupMiddleware<Guid>.IHandler>(
                                        s.GetRequiredService<Handler1>()
                                        )
                                    ),
                                s.GetRequiredService<ITransactionManager>()
                                );

                            await middleware.HandleRangeAsync(ids, t);

                            var result = Interlocked.Add(ref processed, ids.Sum(e => e.Count));
                            System.Diagnostics.Debug.WriteLine($"Processed: {result}");
                            if (result == total)
                            {
                                waitComplete.TrySetResult();
                            }
                        }),
                    bufferLimit: total,
                    processCountLimiter: limit,
                    dbPort: postgreSqlContainer.GetMappedPublicPort(),
                    useMemory: useMemory
                    );

                serviceCollection.AddSingleton(
                    new ProcessRegistryDto(
                        new ProcessTypeDto(0, 0),
                        0
                        ));

                serviceProvider = serviceCollection.BuildServiceProvider();
            }

            try
            {
                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<Process1Repository>();
                    var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    foreach (var elem in Enumerable.Repeat(0, total).Chunk(500))
                    {
                        foreach (var elem2 in elem)
                        {
                            await repo.CreateAsync(
                                0,
                                0,
                                default);
                        }
                        await appDbContext.SaveChangesAsync();
                        appDbContext.ChangeTracker.Clear();
                    }                    
                }
                GC.Collect(3, GCCollectionMode.Forced);

                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    using var cancel = new CancellationTokenSource();

                    var runner = serviceProvider.GetRequiredService<IProcessRunner>();
                    await runner.BuildHandler();

                    var stopwatch = Stopwatch.StartNew();

                    var runnerTask = Task.Run(
                        async () => await runner.RunAsync(cancel.Token));

                    await waitComplete.Task;

                    stopwatch.Stop();
                    cancel.Cancel();

                    var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var notCompleteCount = await appDbContext.Process
                        .CountAsync(e => e.Status != ProcessStatusEnum.Complete);

                    Assert.AreEqual(0, notCompleteCount);

                    return stopwatch;
                }
            }
            finally 
            {
                await postgreSqlContainer.DisposeAsync();
            }
        }

        public async Task<Stopwatch> Test2Async(
            bool useMemory,
            bool useDbOptimizations,
            int total, 
            int parallelism)
        {
            IServiceProvider serviceProvider;
            var waitComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var processed = 0;
            PostgreSqlContainer postgreSqlContainer;
            {
                var postgresBuilder = new PostgreSqlBuilder()
                    .WithImage("postgres:18");

                postgreSqlContainer = postgresBuilder.Build();
                await postgreSqlContainer.StartAsync();

                await new DbInit().InitAllAsync(postgreSqlContainer, useDbOptimizations: useDbOptimizations);

                var serviceCollection = new ServiceCollection();

                ModelRegistry.Registry(
                    serviceCollection,
                    new ProcessRunner<Guid>.OptionsDto(
                        SelectBatchLimit: 1000,
                        selectEmptyTimeout: TimeSpan.FromSeconds(4),
                        BatchLimit: parallelism,
                        BatchTimeout: TimeSpan.FromSeconds(2)),
                    ModelRegistry.Selector1(
                        TimeSpan.FromHours(1)),
                    (s) => new TestMiddleware(
                            s,
                            async (s, ids, t) =>
                            {
                                var middleware = new SizeMiddleware<Guid>(
                                    s,
                                    (s) => new ExecuteParallelMiddleware<Guid>(
                                        s,
                                        (s, _) => new TransactionMiddleware<Guid>(
                                            s,
                                            (s) => new ExecuteStepByStepGroupMiddleware<Guid>(
                                                s.GetRequiredService<IServiceProvider>(),
                                                s.GetRequiredService<IIsolationService>(),
                                                s.GetRequiredService<IProcessSetter>(),
                                                s.GetRequiredService<IWakeUpService<Guid>>(),
                                                (s) => ValueTask.FromResult<ExecuteStepByStepGroupMiddleware<Guid>.IHandler>(
                                                    s.GetRequiredService<Handler2>()
                                                    )
                                                ),
                                            s.GetRequiredService<ITransactionManager>()
                                            ),
                                        degreeOfParallelism: (e) => e.Count
                                        ),
                                    chunkSize: 1);

                                await middleware.HandleRangeAsync(ids, t);

                                var result = Interlocked.Add(ref processed, ids.Sum(e => e.Count));
                                System.Diagnostics.Debug.WriteLine($"Processed: {result}");
                                if (result == total)
                                {
                                    waitComplete.TrySetResult();
                                }
                            }),
                    bufferLimit: total,
                    processCountLimiter: parallelism,
                    dbPort: postgreSqlContainer.GetMappedPublicPort(),
                    useMemory: useMemory);

                serviceCollection.AddSingleton(
                    new ProcessRegistryDto(
                        new ProcessTypeDto(0, 0),
                        0
                        ));

                serviceProvider = serviceCollection.BuildServiceProvider();
            }

            try
            {
                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<Process1Repository>();
                    var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    foreach (var elem in Enumerable.Repeat(0, total).Chunk(500))
                    {
                        foreach (var elem2 in elem)
                        {
                            await repo.CreateAsync(
                                0,
                                0,
                                default);
                        }
                        await appDbContext.SaveChangesAsync();
                        appDbContext.ChangeTracker.Clear();
                    }                    
                }
                GC.Collect(3, GCCollectionMode.Forced);

                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    using var cancel = new CancellationTokenSource();

                    var runner = serviceProvider.GetRequiredService<IProcessRunner>();
                    await runner.BuildHandler();

                    var stopwatch = Stopwatch.StartNew();

                    var runnerTask = Task.Run(
                        async () => await runner.RunAsync(cancel.Token));

                    await waitComplete.Task;

                    stopwatch.Stop();
                    cancel.Cancel();

                    var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var notCompleteCount = await appDbContext.Process
                        .CountAsync(e => e.Status != ProcessStatusEnum.Complete);

                    Assert.AreEqual(0, notCompleteCount);

                    return stopwatch;
                }
            }
            finally
            {
                await postgreSqlContainer.DisposeAsync();
            }
        }
    }
}
