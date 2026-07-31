using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Providers;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners
{
    public class InMemoryQueueProcessRunner<TId>
        : IInMemoryQueueProcessRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsDto _options;     

        private ConcurrentDictionary<Guid, Task> RunningTasks { get; }
            = new ConcurrentDictionary<Guid, Task>();

        public InMemoryQueueProcessRunner(
            IServiceProvider serviceProvider,
            OptionsDto options
            )
        {
            _serviceProvider = serviceProvider;
            _options = options;        
        }
        
        public async Task BuildHandler()
        {
            await using (var scope = _serviceProvider.CreateAsyncScope())
            {
                _options.SelectFactory(scope.ServiceProvider);
                _options.RootMiddlewareFactory(scope.ServiceProvider);
            }
        }

        public async Task RunAsync(
            bool oneCycle,
            CancellationToken cancellationToken)
        {
            static async Task RunSelector(
                IServiceProvider serviceProvider,
                bool oneCycle,
                CancellationToken cancellationToken)
            {
                var options = serviceProvider.GetRequiredService<OptionsDto>();
                var buffer = serviceProvider.GetRequiredService<IInMemoryQueueProcessRunner.ILocalProcessBufferService<TId>>();

                // Для пробуждения, если очередь опустела.
                TaskCompletionSource wakeUpTask;
                {
                    wakeUpTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    using var _ = buffer.AddEmptyHandler(
                        (b) => wakeUpTask.TrySetResult());
                }

                var registrations = serviceProvider
                    .GetRequiredService<IProcessRegistry>()
                    .All();

                while (true)
                {
                    await using (var scope = serviceProvider.CreateAsyncScope())
                    {
                        var freeSpace = buffer.FreeSpace;

                        var select = options.SelectFactory(scope.ServiceProvider);
                        var reservationProvider = scope.ServiceProvider.GetRequiredService<IProcessReservationProvider<TId>>();

                        var selectContext = new LinkContainer<(object? _, int BatchSize)>(
                            ((object? _, int BatchSize))(null, Math.Min(freeSpace, options.SelectBatchLimit))
                            );

                        wakeUpTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                        try
                        {
                            await foreach (var elem in select.SelectProcessIdsForAsyncProcessingAsync(
                                selectContext,
                                registrations,
                                cancellationToken)
                                .WithCancellation(cancellationToken))
                            {
                                // Выборка пустая.
                                if (elem.Count == 0)
                                {
                                    break;
                                }

                                var produceResult = buffer.TryProduce(elem);

                                // Буфер заполнен.
                                if (produceResult.ids.Count != 0)
                                {
                                    // Очередь заполнена, разблокируем процессы, которын не попали в буфер,
                                    // чтобы их могли взять в обработку другие экземпляры.
                                    await reservationProvider.UnreserveAsync(
                                        produceResult.ids.Select(e => e.Id).ToArray(),
                                        cancellationToken);

                                    break;
                                }

                                selectContext.Data = (null, Math.Min(freeSpace, options.SelectBatchLimit));

                                if (oneCycle)
                                {
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                            {
                                throw;
                            }

                            if (oneCycle)
                            {
                                throw;
                            }

                            // TODO: log

                            await Task.Delay(options.SelectorExceptionDelay, cancellationToken);
                        }

                        if (oneCycle)
                        {
                            break;
                        }

                        await TimeoutHelper.ExecuteWithTimeoutAsync(
                            wakeUpTask,
                            options.selectEmptyTimeout,
                            static async (p) => await p.Task
                            );
                    }
                }
            }

            static async ValueTask<bool> QueueConsumerAsync(
                IServiceProvider serviceProvider,
                ConcurrentDictionary<Guid, Task> runningTasks,
                bool oneCycle,
                CancellationToken cancellationToken)
            {
                var options = serviceProvider.GetRequiredService<OptionsDto>();
                var executeLimiter = serviceProvider.GetRequiredService<IExecuteLimiterInvoker>();
                var processCountLimiter = serviceProvider.GetRequiredService<ProcessCountLimiter>();
                var _buffer = serviceProvider.GetRequiredService<IInMemoryQueueProcessRunner.ILocalProcessBufferService<TId>>();

                await executeLimiter.WaitNextAsync(cancellationToken);

                var batch = await _buffer.ConsumeBatch2Async(
                    options.BatchLimit,
                    options.BatchTimeout,
                    cancellationToken);

                if (batch.Count == 0)
                {
                    return true;
                }

                processCountLimiter.Start(batch.Count);
                var taskId = Guid.NewGuid();
                var task = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await using (var scope = serviceProvider.CreateAsyncScope())
                            {
                                var handler = options.RootMiddlewareFactory(scope.ServiceProvider);
                                await handler.HandleRangeAsync([batch], cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                            {
                                throw;
                            }

                            if (oneCycle)
                            {
                                throw;
                            }

                            // В целом предпологается, что хендел не должен допускать сюда Exception.
                            // TODO: log
                        }
                        finally
                        {
                            processCountLimiter.Stop(batch.Count);
                            runningTasks.TryRemove(taskId, out _);
                        }
                    }
                    );
                runningTasks.TryAdd(taskId, task);

                if (oneCycle)
                {
                    return false;
                }

                return true;
            }


            // 1) Db selector.
            {
                var selectTaskId = Guid.NewGuid();
                var selectTask = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await RunSelector(
                                _serviceProvider,
                                oneCycle,
                                cancellationToken);
                        }
                        finally
                        {
                            RunningTasks.TryRemove(selectTaskId, out _);
                        }
                    }
                    );
                RunningTasks.TryAdd(selectTaskId, selectTask);
            }

            // 2) InMemory queue consumer.

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await QueueConsumerAsync(
                    _serviceProvider, 
                    RunningTasks,
                    oneCycle, 
                    cancellationToken);

                if (!result)
                {
                    break;
                }
            }

            await WaitRunningTasksAsync(cancellationToken);
        }

        public async Task WaitRunningTasksAsync(CancellationToken cancellationToken)
        {
            await Task.WhenAll(RunningTasks.Values);
        }

        public async ValueTask DisposeAsync()
        {
            await WaitRunningTasksAsync(default);
            RunningTasks.Clear();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="SelectBatchLimit">Ограничение размера выборки одного запроса к хранилищу процессов.</param>
        /// <param name="selectEmptyTimeout">Задержка, если процессов в хранилище нет или очередь заполнена.</param>
        /// <param name="BatchLimit">Ограничение размера батча выборки из InMemory батча и отправки в обработку.</param>
        /// <param name="BatchTimeout">Ограничения по времени на выборку батча (если батч не наполняется полностью).</param>
        public readonly record struct OptionsDto(
            int SelectBatchLimit,
            TimeSpan selectEmptyTimeout,
            int BatchLimit,
            TimeSpan BatchTimeout,
            TimeSpan SelectorExceptionDelay,
            Func<IServiceProvider, IInMemoryQueueProcessRunner.ISelectQuery<TId>> SelectFactory,
            Func<IServiceProvider, IProcessHandlerMiddleware<TId>> RootMiddlewareFactory
            );
    }
}
