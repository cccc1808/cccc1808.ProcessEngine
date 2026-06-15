using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.QueueModule;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners
{
    public class ParallelLimitProcessQueueRunner<TId> 
        : IParallelLimitProcessQueueRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsDto _options;

        private readonly ConcurrentDictionary<Guid, Task> _runningTasks
            = new ConcurrentDictionary<Guid, Task>();

        public ParallelLimitProcessQueueRunner(
            IServiceProvider serviceProvider,
            OptionsDto options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        #region IProcessRunner

        public async Task BuildHandler()
        {
            await using (var scope = _serviceProvider.CreateAsyncScope())
            {
                // _options.RangeMiddlewareFactory(scope.ServiceProvider); TODO: включить проверку.
                _options.SignleMiddlewareFactory(scope.ServiceProvider);
            }
        }

        public async Task RunAsync(bool executeOne, CancellationToken cancellationToken)
        {
            static async Task<ICollection<ProcessAsyncExecuteMessageDto<TId>>> SelectAsync(
                IServiceProvider serviceProvider,
                OptionsDto options,
                SemaphoreSlim parallelLimiter,
                bool oneExecute,
                CancellationToken cancellationToken)
            {
                var queueProviderFactory = serviceProvider.GetRequiredService<IQueueProviderFactory>();

                // Ждем освобождения хотя бы одного слота.
                await parallelLimiter.WaitAsync(cancellationToken);

                var consumer = await QueuePatternHelper.ConnectOrReconnectConsumerAsync(
                    queueProviderFactory,
                    options.ExceptionDelay,
                    consumer: null,
                    queueName: options.QueueName,
                    oneExecute: oneExecute,
                    (ex) => { /* TODO: */ },
                    cancellationToken);

                var totalCounter = new LinkContainer<int>(0);
                var rangeCounter = new LinkContainer<int>(0);
                var result = new List<ProcessAsyncExecuteMessageDto<TId>>();

                try
                {
                    await consumer.ConsumeBatchAsync(
                        (options, totalCounter, rangeCounter, parallelLimiter, result), 
                        options.ConsumeTimeout, 
                        static (p, m) => 
                        {
                            var content = m.Body.Deserialize<ProcessAsyncExecuteMessageDto<TId>>();
                            p.result.Add(content);

                            if (!content.IsRangeProcess)
                            {
                                p.totalCounter.Data++;
                            }
                            else 
                            {
                                p.rangeCounter.Data++;

                                if (p.rangeCounter.Data >= p.options.TransactionUpdateLimit)
                                {
                                    p.totalCounter.Data++;
                                    p.rangeCounter.Data = 0;
                                }
                            }

                            // Есть свободные слоты (и не timeout) читаем дальше.
                            var needContinue = p.totalCounter.Data < p.parallelLimiter.CurrentCount;
                            return needContinue;
                        }, 
                        cancellationToken);
                    
                    // TODO: автокоммит сразу.
                    await consumer.CommitAsync(cancellationToken);
                }
                catch(Exception)
                {
                    // TODO: log;

                    consumer = await QueuePatternHelper.ConnectOrReconnectConsumerAsync(
                        queueProviderFactory,
                        options.ExceptionDelay,
                        consumer: consumer,
                        queueName: options.QueueName,
                        oneExecute: oneExecute,
                        (ex) => { /* TODO: */ },
                        cancellationToken);
                }
                finally
                {
                    parallelLimiter.Release();
                }

                return result;
            }

            static async Task ExecuteHandlerAsync(
                IServiceProvider serviceProvider,
                OptionsDto options,
                SemaphoreSlim parallelLimiter,
                ConcurrentDictionary<Guid, Task> tasks,
                ICollection<ProcessAsyncExecuteMessageDto<TId>> selectData,
                CancellationToken cancellationToken)
            {
                static async Task ExecuteRangeHandlerAsync(
                    IServiceProvider serviceProvider,
                    OptionsDto options,
                    ProcessAsyncExecuteMessageDto<TId>[] group,
                    CancellationToken cancellationToken)
                {
                    var handler = options.RangeMiddlewareFactory(serviceProvider);

                    await handler.HandleRangeAsync(
                        [group.Select(e => e.ProcessInstanceInfo).ToArray()],
                        cancellationToken);
                }

                static async Task ExecuteSingleHandlerAsync(
                    IServiceProvider serviceProvider,
                    OptionsDto options,
                    ProcessAsyncExecuteMessageDto<TId> process,
                    CancellationToken cancellationToken)
                {
                    var handler = options.SignleMiddlewareFactory(serviceProvider);

                    await handler.HandleRangeAsync(
                        [[process.ProcessInstanceInfo]],
                        cancellationToken);
                }

                // Группировка по ключу (Info: точка агрегации):
                // Если триггер групповой, то обработку будет одним батчем (одной транзакцией).
                var groupByRange = selectData.GroupBy(e => e.IsRangeProcess);

                foreach (var group in groupByRange)
                {
                    if (!group.Key)
                    {
                        // Режим запуска: 1 task - 1 транзакция - 1 процесс
                        foreach (var elem in group)
                        {
                            await parallelLimiter.WaitAsync(cancellationToken);
                            var id = Guid.NewGuid();

                            var task = Task.Run(
                                async () =>
                                {
                                    try
                                    {
                                        await using (var scope = serviceProvider.CreateAsyncScope())
                                        {
                                            await ExecuteSingleHandlerAsync(
                                                scope.ServiceProvider,
                                                options,
                                                elem,
                                                cancellationToken
                                                );
                                        }
                                    }
                                    finally
                                    {
                                        tasks.TryRemove(id, out _);
                                        parallelLimiter.Release();
                                    }
                                });
                            tasks.TryAdd(id, task);
                        }
                    }
                    else
                    {
                        // Режим запуска: 1 task - 1 transaction - N процессов.
                        foreach (var batch in group
                            .OrderBy(e => e.ProcessInstanceInfo.ProcessType.ProcessType)
                            .Chunk(options.TransactionUpdateLimit))
                        {
                            await parallelLimiter.WaitAsync(cancellationToken);
                            var id = Guid.NewGuid();

                            var task = Task.Run(
                                async () =>
                                {
                                    try
                                    {
                                        await using (var scope = serviceProvider.CreateAsyncScope())
                                        {
                                            await ExecuteRangeHandlerAsync(
                                                serviceProvider,
                                                options,
                                                batch,
                                                cancellationToken);
                                        }
                                    }
                                    finally
                                    {
                                        tasks.TryRemove(id, out _);
                                        parallelLimiter.Release();
                                    }
                                });
                            tasks.TryAdd(id, task);
                        }
                    }
                }
            }

            using var parallelLimiter = new SemaphoreSlim(
                _options.DbExecuteParallelismLimit
                + 1 // на SelectAsync
                );

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    ICollection<ProcessAsyncExecuteMessageDto<TId>> selectData;
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        selectData = await SelectAsync(
                            scope.ServiceProvider,
                            _options,
                            parallelLimiter,
                            oneExecute: executeOne,
                            cancellationToken);
                    }

                    if (!selectData.Any())
                    {
                        break;
                    }

                    await ExecuteHandlerAsync(
                        _serviceProvider,
                        _options,
                        parallelLimiter,
                        _runningTasks,
                        selectData,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                    {
                        throw;
                    }

                    // Log

                    if (executeOne)
                    {
                        throw;
                    }

                    await Task.Delay(_options.ExceptionDelay, cancellationToken);
                }

                if (executeOne)
                {
                    break;
                }
            }

            await WaitRunningTasksAsync(cancellationToken);
        }

        public async Task WaitRunningTasksAsync(CancellationToken cancellationToken)
        {
            await Task.WhenAll(_runningTasks.Values);
            _runningTasks.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await WaitRunningTasksAsync(CancellationToken.None);
            }
            catch
            {
                // Log
            }
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="SelectBatchLimit">Ограничение размера выборки одного запроса к хранилищу процессов.</param>
        /// <param name="selectEmptyTimeout">Задержка, если процессов в хранилище нет или очередь заполнена.</param>
        /// <param name="BatchLimit">Ограничение размера батча выборки из InMemory батча и отправки в обработку.</param>
        /// <param name="BatchTimeout">Ограничения по времени на выборку батча (если батч не наполняется полностью).</param>
        public class OptionsDto
        {
            public required string QueueName { get; set; }

            public TimeSpan ConsumeTimeout { get; set; }
                = TimeSpan.FromSeconds(0.1);

            public TimeSpan ExceptionDelay { get; set; }
                = TimeSpan.FromSeconds(1);

            public int DbExecuteParallelismLimit { get; set; }
                = 10;

            public int TransactionUpdateLimit { get; set; }
                = 250;

            public required Func<IServiceProvider, IProcessHandlerMiddleware<TId>> RangeMiddlewareFactory { get; set; }

            public required Func<IServiceProvider, IProcessHandlerMiddleware<TId>> SignleMiddlewareFactory { get; set; }
        }
    }
}
