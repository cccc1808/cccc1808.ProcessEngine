using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Services.Limiter;
using cccc1808.ProcessEngine.Model.Abstract.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Query;
using cccc1808.ProcessEngine.Model.Common;
using cccc1808.ProcessEngine.Model.Implementation.Limiter;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.Runners
{
    public class ProcessRunner<TId>
        : IProcessRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsDto _options;
        private readonly ILocalProcessBufferService<TId> _buffer;
        private readonly IExecuteLimiterInvoker _executeLimiter;
        private readonly ProcessCountLimiter _processCountLimiter;
        private readonly Func<IServiceProvider, IProcessSelectQuery<TId>> _selectFactory;
        private readonly Func<IServiceProvider, IProcessHandlerMiddleware<TId>> _rootMiddlewareFactory;

        public ProcessRunner(
            IServiceProvider serviceProvider,
            OptionsDto options,
            ILocalProcessBufferService<TId> buffer,
            IExecuteLimiterInvoker executeLimiter,
            ProcessCountLimiter processCountLimiter,
            Func<IServiceProvider, IProcessSelectQuery<TId>> selectFactory,
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

        private List<Task> RunningTasks { get; } 
            = new List<Task>();        

        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(RunningTasks);
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

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            {
                var selectTask = Task.Run(
                    async () =>
                    {
                        // Для пробуждения, если очередь опустела.
                        var wakeUpTask = new TaskCompletionSource(
                            TaskCreationOptions.RunContinuationsAsynchronously);

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
                                var selectContext = new IProcessSelectQuery<TId>.ContextDto()
                                {
                                    BatchSize = Math.Min(freeSpace, _options.SelectBatchLimit)
                                };

                                wakeUpTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                                await foreach (var elem in select.SelectAsync(
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

                                    ;
                                    var produceResult = _buffer.TryProduce(elem);
                                    freeSpace = produceResult.FreeSpace;

                                    // Буфер заполнен.
                                    if (freeSpace == 0)
                                    {
                                        // Очередь заполнена, разблокируем процессы, которын не попали в буфер,
                                        // чтобы их могли взять в обработку другие экземпляры.
                                        await select.UnlockSelectAsync(
                                            produceResult.ids,
                                            cancellationToken);

                                        break;
                                    }

                                    selectContext.BatchSize = Math.Min(freeSpace, _options.SelectBatchLimit);
                                }

                                await TimeoutHelper.ExecuteWithTimeoutAsync(
                                    wakeUpTask,
                                    _options.selectEmptyTimeout,
                                    static async (p, t) => await p.Task,
                                    cancellationToken
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
            }

            cancellationToken.ThrowIfCancellationRequested();
        }


        public readonly record struct OptionsDto(
            int SelectBatchLimit,
            TimeSpan selectEmptyTimeout,
            int BatchLimit,
            TimeSpan BatchTimeout
            );
    }
}
