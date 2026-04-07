using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners
{
    /// <summary>
    /// TODO: кривая реализация буфер выполняющихся задач.
    /// TODO: подумать нужна ли InMemory queue или может синхронно вычитывать и запускать 
    /// (если лимит, то сбрасываем select lock и спим пока не снимется лимит)
    /// (по хорошему нужно мерить производительность разных реализаций, но пока не буду).
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class ProcessRunner<TId>
        : IProcessRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsDto _options;
        private readonly ILocalProcessBufferService<TId> _buffer;
        private readonly IExecuteLimiterInvoker _executeLimiter;
        private readonly ProcessCountLimiter _processCountLimiter;
        private readonly Func<IServiceProvider, IProcessAsyncProcessingSelectQuery<TId>> _selectFactory;
        private readonly Func<IServiceProvider, IProcessHandlerMiddleware<TId>> _rootMiddlewareFactory;

        private List<Task> RunningTasks { get; }
            = new List<Task>();

        public ProcessRunner(
            IServiceProvider serviceProvider,
            OptionsDto options,
            ILocalProcessBufferService<TId> buffer,
            IExecuteLimiterInvoker executeLimiter,
            ProcessCountLimiter processCountLimiter,
            Func<IServiceProvider, IProcessAsyncProcessingSelectQuery<TId>> selectFactory,
            Func<IServiceProvider, IProcessHandlerMiddleware<TId>> rootMiddlewareFactory
            )
        {
            _serviceProvider = serviceProvider;
            _options = options;
            _buffer = buffer;
            _executeLimiter = executeLimiter;
            _processCountLimiter = processCountLimiter;
            _selectFactory = selectFactory;
            _rootMiddlewareFactory = rootMiddlewareFactory;           
        }          

        public async ValueTask DisposeAsync()
        {
            await WaitRunningTasksAsync(default);
            RunningTasks.Clear();
        }

        public async Task BuildHandler()
        {
            await using (var scope = _serviceProvider.CreateAsyncScope())
            {
                _selectFactory(scope.ServiceProvider);
                var middleware = _rootMiddlewareFactory(scope.ServiceProvider);
            }
        }

        public async Task RunAsync(
            bool oneCycle,
            CancellationToken cancellationToken)
        {
            {
                var selectTask = Task.Run(
                    async () =>
                    {
                        // Для пробуждения, если очередь опустела.
                        var wakeUpTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                        using var _ = _buffer.AddEmptyHandler(
                            (b) => wakeUpTask.TrySetResult());

                        var registrations = _serviceProvider
                            .GetRequiredService<IProcessRegistry>()
                            .All();

                        while (true)
                        {
                            await using (var scope = _serviceProvider.CreateAsyncScope())
                            {
                                var freeSpace = _buffer.FreeSpace;

                                var select = _selectFactory(scope.ServiceProvider);


                                var selectContext = new LinkContainer<(object? _, int BatchSize)>(
                                    (null, Math.Min(freeSpace, _options.SelectBatchLimit))
                                    );

                                wakeUpTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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

                                    var produceResult = _buffer.TryProduce(elem);

                                    // Буфер заполнен.
                                    if (produceResult.ids.Count != 0)
                                    {
                                        // Очередь заполнена, разблокируем процессы, которын не попали в буфер,
                                        // чтобы их могли взять в обработку другие экземпляры.
                                        await select.UnlockSelectAsync(
                                            produceResult.ids,
                                            cancellationToken);

                                        break;
                                    }

                                    selectContext.Data = (null, Math.Min(freeSpace, _options.SelectBatchLimit));
                                }

                                if (oneCycle)
                                {
                                    break;
                                }

                                await TimeoutHelper.ExecuteWithTimeoutAsync(
                                    wakeUpTask,
                                    _options.selectEmptyTimeout,
                                    static async (p) => await p.Task
                                    );
                            }
                        }
                    }
                    );

                RunningTasks.Add(selectTask);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await _executeLimiter.WaitNextAsync(cancellationToken);

                var batch = await _buffer.ConsumeBatch2Async(
                    _options.BatchLimit,
                    _options.BatchTimeout,
                    cancellationToken);

                if (batch.Count == 0)
                {
                    continue;
                }

                _processCountLimiter.Start(batch.Count);

                var task = Task.Run(
                    async () => 
                    {
                        try 
                        {
                            await using (var scope = _serviceProvider.CreateAsyncScope())
                            {
                                var handler = _rootMiddlewareFactory(scope.ServiceProvider);
                                await handler.HandleRangeAsync([batch], cancellationToken);
                            }
                        }
                        finally 
                        {
                            _processCountLimiter.Stop(batch.Count);
                        }
                    }
                    );
                RunningTasks.Add(task);   
                
                if (oneCycle)
                {
                    break;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public async Task WaitRunningTasksAsync(CancellationToken cancellationToken)
        {
            await Task.WhenAll(RunningTasks);
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
            TimeSpan BatchTimeout
            );
    }
}
