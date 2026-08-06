using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners
{
    public class QueueProcessRunner<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsDto _options;

        public QueueProcessRunner(
            IServiceProvider serviceProvider,
            OptionsDto options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public async Task RunRangeExecuteAsync(
            bool executeOne, 
            CancellationToken cancellationToken)
        {
            static async Task<ICollection<IProcessQueueProvider<TId>.MessageDto>> ConsumeAsync(
                IServiceProvider serviceProvider,
                OptionsDto options,
                SemaphoreSlim parallelLimiter,
                CancellationToken cancellationToken)
            {
                var queueProvider = serviceProvider.GetRequiredService<IProcessQueueProvider<TId>>();

                // Ждем освобождения хотя бы одного слота.
                await parallelLimiter.WaitAsync(cancellationToken);
                try
                {
                    var freeSlots = parallelLimiter.CurrentCount;
                    var batchSize = freeSlots * options.TransactionUpdateLimit;

                    var messages = await queueProvider.ConsumeRangeAsync(batchSize, freeSlots, options.RangeExecute_ConsumeTimeout, cancellationToken);
                    return messages;
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
                ICollection<IProcessQueueProvider<TId>.MessageDto> selectData,
                CancellationToken cancellationToken)
            {
                var groupByRange = selectData.GroupBy(e => (e.Registry.ProcessType, e.Registry.ProcessType.ProcessVersion));

                foreach (var group in groupByRange)
                {
                    // Режим запуска: 1 task - 1 transaction - N процессов.
                    foreach (var batch in group.Chunk(options.TransactionUpdateLimit))
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
                                        var handler = options.RangeExecute_MiddlewareFactory(serviceProvider);

                                        await handler.HandleRangeAsync(
                                            [batch.Select(e => new ProcessInstanceInfoDto<TId>(e.ProcessId, e.Registry.ProcessType, e.Registry.Priority)).ToArray()],
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

            using var parallelLimiter = new SemaphoreSlim(
                _options.RangeExecute_ParallelismLimit
                + 1 // на ConsumeAsync
                );
            var runningTasks = new ConcurrentDictionary<Guid, Task>();

            try
            {
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        ICollection<IProcessQueueProvider<TId>.MessageDto> selectData;
                        await using (var scope = _serviceProvider.CreateAsyncScope())
                        {
                            selectData = await ConsumeAsync(
                                scope.ServiceProvider,
                                _options,
                                parallelLimiter,
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
                            runningTasks,
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
            }
            finally 
            {
                await Task.WhenAll(runningTasks.Values);
            }            
        }

        public async Task RunSingleExecuteAsync(
            bool executeOne,
            CancellationToken cancellationToken)
        {
            static async Task<ICollection<IProcessQueueProvider<TId>.MessageDto>> ConsumeAsync(
                IServiceProvider serviceProvider,
                OptionsDto options,
                SemaphoreSlim parallelLimiter,
                CancellationToken cancellationToken)
            {
                var queueProvider = serviceProvider.GetRequiredService<IProcessQueueProvider<TId>>();

                // Ждем освобождения хотя бы одного слота.
                await parallelLimiter.WaitAsync(cancellationToken);
                try
                {
                    var freeSlots = parallelLimiter.CurrentCount;
                    var batchSize = freeSlots * options.TransactionUpdateLimit;

                    var messages = await queueProvider.ConsumeSignleAsync(batchSize, options.RangeExecute_ConsumeTimeout, cancellationToken);
                    return messages;
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
                ICollection<IProcessQueueProvider<TId>.MessageDto> selectData,
                CancellationToken cancellationToken)
            {
                foreach (var elem in selectData)
                {
                    // Режим запуска: 1 task - 1 транзакция - 1 процесс
                    await parallelLimiter.WaitAsync(cancellationToken);
                    var id = Guid.NewGuid();

                    var task = Task.Run(
                        async () =>
                        {
                            try
                            {
                                await using (var scope = serviceProvider.CreateAsyncScope())
                                {
                                    var handler = options.SingleExecute_MiddlewareFactory(serviceProvider);

                                    await handler.HandleRangeAsync(
                                        [[new ProcessInstanceInfoDto<TId>(elem.ProcessId, elem.Registry.ProcessType, elem.Registry.Priority)]],
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

             using var parallelLimiter = new SemaphoreSlim(
                _options.SingleExecute_ParallelismLimit
                + 1 // на ConsumeAsync
                );
            var runningTasks = new ConcurrentDictionary<Guid, Task>();

            try
            {

                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        ICollection<IProcessQueueProvider<TId>.MessageDto> selectData;
                        await using (var scope = _serviceProvider.CreateAsyncScope())
                        {
                            selectData = await ConsumeAsync(
                                scope.ServiceProvider,
                                _options,
                                parallelLimiter,
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
                            runningTasks,
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
            }
            finally 
            {
                await Task.WhenAll(runningTasks.Values);
            }
        }

        public async Task DbSelectExecuteAsync(
            bool executeOne,
            CancellationToken cancellationToken)
        {
            var processRegistry = _serviceProvider.GetRequiredService<IProcessRegistry>();

            var parallelLimit = new SemaphoreSlim(_options.DbSelect_ParallilLimit);

            var executeOneTriggered = false;
            var tasks = new ConcurrentDictionary<Guid, Task>();

            foreach (var elem in processRegistry.All())
            {
                var id = Guid.NewGuid();
                var selectTask = Task.Run(
                    async () =>
                    {
                        try
                        {
                            TimeSpan? timeout = null;

                            while (true)
                            {
                                if (executeOne && executeOneTriggered)
                                {
                                    return;
                                }

                                if (timeout.HasValue)
                                {
                                    await Task.Delay(timeout.Value, cancellationToken);
                                    timeout = null;
                                }

                                await parallelLimit.WaitAsync();

                                // TODO: обработка блокировок кластера. (резервирование типа хендлера на ноде).

                                try
                                {
                                    using (var scope = _serviceProvider.CreateAsyncScope())
                                    {
                                        var options = scope.ServiceProvider.GetRequiredService<OptionsDto>();
                                        var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
                                        var query = scope.ServiceProvider.GetRequiredService<IQueueProcessRunnerQuery<TId>>();
                                        var processQueueContext = scope.ServiceProvider.GetRequiredService<IProcessQueueContext<TId>>();

                                        var context = query.InitContext(_options.DbSelect_Options, elem);

                                        while (true)
                                        {
                                            var selectData = await query.ExecuteAsync(context, cancellationToken);

                                            if (!selectData.Any())
                                            {
                                                timeout = options.DbSelect_EmptyDelay;
                                                break;
                                            }

                                            var queueIsFull = await processQueueContext.ProcessFromSelectorAsync(
                                                selectData
                                                    .Select(e => IProcessQueueContext<TId>.ProcessDto.ProcessFromSelector(
                                                        e.ProcessId,
                                                        elem)
                                                    )
                                                    .ToArray(),
                                                reserveDate: elem.IsSignleExecuteProcess
                                                    ? dateTime.UtcNow + options.DbSelect_RangeReservationTimeout
                                                    : dateTime.UtcNow + options.DbSelect_SingleReservationTimeout,
                                                cancellationToken
                                                );

                                            Interlocked.CompareExchange(ref executeOneTriggered, true, false);

                                            if (queueIsFull)
                                            {
                                                timeout = options.DbSelect_QueueIsFullTimeout;
                                                break;
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                                    {
                                        throw;
                                    }

                                    if (executeOne)
                                    {
                                        throw;
                                    }

                                    // TODO: log;

                                    timeout = _options.ExceptionDelay;
                                }
                                finally
                                {
                                    parallelLimit.Release();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                            {
                                throw;
                            }

                            if (executeOne)
                            {
                                throw;
                            }

                            // TODO: log;
                        }
                        finally
                        {
                            tasks.TryRemove(id, out _);
                        }
                    }
                    );

                tasks.TryAdd(id, selectTask);
            }

            await Task.WhenAll(tasks.Values);
        }

        public class OptionsDto
        {
            public int TransactionUpdateLimit { get; set; }
                = 50;

            public int RangeExecute_ParallelismLimit { get; set; }
                = 20;

            public TimeSpan RangeExecute_ConsumeTimeout { get; set; }
                = TimeSpan.FromSeconds(0.1);

            public required Func<IServiceProvider, IProcessHandlerMiddleware<TId>> RangeExecute_MiddlewareFactory { get; set; }

            public int SingleExecute_ParallelismLimit { get; set; }
                = 3;

            public TimeSpan SingleExecute_ConsumeTimeout { get; set; }
                = TimeSpan.FromSeconds(0.1);

            public required Func<IServiceProvider, IProcessHandlerMiddleware<TId>> SingleExecute_MiddlewareFactory { get; set; }

            public TimeSpan ExceptionDelay { get; set; }
                = TimeSpan.FromSeconds(5);

            public int DbSelect_ParallilLimit { get; set; }
                = 4;

            public required IQueueProcessRunnerQuery<TId>.IOptions DbSelect_Options { get; set; }

            public TimeSpan DbSelect_EmptyDelay { get; set; }
                = TimeSpan.FromSeconds(5);

            public TimeSpan DbSelect_QueueIsFullTimeout { get; set; }
                = TimeSpan.FromSeconds(10);

            public TimeSpan DbSelect_RangeReservationTimeout { get; set; }
                = TimeSpan.FromSeconds(60);

            public TimeSpan DbSelect_SingleReservationTimeout { get; set; }
                = TimeSpan.FromSeconds(30);
        }
    }
}
