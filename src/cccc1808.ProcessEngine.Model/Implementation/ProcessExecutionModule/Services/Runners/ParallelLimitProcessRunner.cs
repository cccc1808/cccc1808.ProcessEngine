using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners
{
    /// <summary>
    /// Реализация раннера процессов без InMemory очереди.
    /// За основу взята текущая реализация <see cref="TriggerRunner{TId}"/>.
    /// Особенности:
    /// * Не использует InMemory очереди (чтобы задача не резервировалась, если нет свободного слота parallel Limit).
    /// * Используется только простой parallel limit (ограничение на количество параллельных task), в отличии от <see cref="InMemoryQueueProcessRunner{TId}"/>.
    /// </summary>
    public class ParallelLimitProcessRunner<TId> 
        : IParallelLimitProcessRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsDto _options;

        private readonly ConcurrentDictionary<Guid, Task> _runningTasks 
            = new ConcurrentDictionary<Guid, Task>();

        public ParallelLimitProcessRunner(
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
                _options.SelectFactory(scope.ServiceProvider);
                // _options.RangeMiddlewareFactory(scope.ServiceProvider); TODO: включить проверку.
                _options.SignleMiddlewareFactory(scope.ServiceProvider);
            }
        }        

        public async Task RunAsync(bool executeOne, CancellationToken cancellationToken)
        {
            static async Task<ICollection<IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto>> SelectAsync(
                IServiceProvider serviceProvider,
                OptionsDto options,
                SemaphoreSlim parallelLimiter,
                IParallelLimitProcessRunner.ISelectQuery<TId>.IContextState selectContext,
                CancellationToken cancellationToken)
            {
                var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                var selectQuery = options.SelectFactory(serviceProvider);

                // Ждем освобождения хотя бы одного слота.
                await parallelLimiter.WaitAsync(cancellationToken);
                try
                {
                    await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                    {
                        selectContext.SetFreeSlots(parallelLimiter.CurrentCount);
                        var result = await selectQuery.SelectForProcessingAsync(
                            selectContext,
                            cancellationToken);

                        await transaction.CommitAsync(cancellationToken);
                        return result;
                    }
                }
                finally
                {
                    parallelLimiter.Release();
                }
            }

            static async Task ExecuteHandlerAsync(
                IServiceProvider serviceProvider,
                OptionsDto options,
                SemaphoreSlim parallelLimiter,
                ConcurrentDictionary<Guid, Task> tasks,
                ICollection<IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto> selectData,
                CancellationToken cancellationToken)
            {
                static async Task ExecuteRangeHandlerAsync(
                    IServiceProvider serviceProvider,
                    OptionsDto options,
                    IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto[] group,
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
                    IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto process,
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

            var selectQuery = _options.SelectFactory(_serviceProvider);

            using var parallelLimiter = new SemaphoreSlim(
                _options.DbExecuteParallelismLimit
                + 1 // на SelectAsync
                );

            var selectContext = selectQuery.BuildContext(_options.SelectOptions);
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    ICollection<IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto> selectData;
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        selectData = await SelectAsync(
                            scope.ServiceProvider,
                            _options,
                            parallelLimiter,
                            selectContext,
                            cancellationToken);
                    }

                    if (!selectData.Any())
                    {
                        await Task.Delay(_options.EmptySelectDelay, cancellationToken);
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
            public TimeSpan ExceptionDelay { get; set; }
                = TimeSpan.FromSeconds(1);

            public TimeSpan EmptySelectDelay { get; set; }
                = TimeSpan.FromSeconds(0.1);

            public IParallelLimitProcessRunner.ISelectQuery<TId>.ISelectOptions SelectOptions { get; set; }

            public int DbExecuteParallelismLimit { get; set; }
                = 10;

            public int TransactionUpdateLimit { get; set; }
                = 250;

            public Func<IServiceProvider, IParallelLimitProcessRunner.ISelectQuery<TId>> SelectFactory { get; set; }

            public Func<IServiceProvider, IProcessHandlerMiddleware<TId>> RangeMiddlewareFactory { get; set; }

            public Func<IServiceProvider, IProcessHandlerMiddleware<TId>> SignleMiddlewareFactory { get; set; }

            public OptionsDto(
                IParallelLimitProcessRunner.ISelectQuery<TId>.ISelectOptions selectOptions,
                Func<IServiceProvider, IParallelLimitProcessRunner.ISelectQuery<TId>> selectFactory, 
                Func<IServiceProvider, IProcessHandlerMiddleware<TId>> rangeMiddlewareFactory,
                Func<IServiceProvider, IProcessHandlerMiddleware<TId>> signleMiddlewareFactory)
            {
                SelectOptions = selectOptions;
                SelectFactory = selectFactory;
                RangeMiddlewareFactory = rangeMiddlewareFactory;
                SignleMiddlewareFactory = signleMiddlewareFactory;
            }
        }
    }
}
