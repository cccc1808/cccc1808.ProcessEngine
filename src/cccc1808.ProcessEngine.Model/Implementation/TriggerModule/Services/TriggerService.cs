using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerService<TId> : ITriggerService
    {
        private readonly IServiceProvider _serviceProvider;

        public TriggerService(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task ConsumerWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken)
        {
            await using (var scope = _serviceProvider.CreateAsyncScope())
            {
                var triggerOptions = scope.ServiceProvider.GetRequiredService<TriggerOptions>();
                var options = scope.ServiceProvider.GetRequiredService<Options>();
                var queueProvider = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var serializer = scope.ServiceProvider.GetRequiredService<IEventJsonSerializer>();

                try
                {
                    var consumer = await queueProvider.GetConsumerAsync(triggerOptions.TriggerEventQueueName, cancellationToken);                    

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var batch = await consumer.ConsumeBatchAsync(
                            options.QueueConsumeBatchLimit,
                            options.QueueConsumeBatchTimeout,
                            cancellationToken);                        

                        if (!batch.Any())
                        {
                            break;
                        }

                        var events = batch
                            .Select(e => serializer.Deserialize(e.Body))
                            .ToArray();

                        // (Info: точка агрегации).
                        // N событий триггера сжимаются в одном действия (1 update).
                        var triggersGroups = events
                            .GroupBy(e => e.TriggerKey)
                            .ToDictionary(e => e.Key, e => e.Select(e => e));

                        await using (var scope2 = scope.ServiceProvider.CreateAsyncScope())
                        {
                            var transactionManager = scope2.ServiceProvider.GetRequiredService<ITransactionManager>();
                            var repository = scope2.ServiceProvider.GetRequiredService<ITriggerRepository<TId>>();
                            var triggerSetter = scope2.ServiceProvider.GetRequiredService<ITriggerSetter<TId>>();

                            await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                            {
                                // Триггеры (используемые для TriggerEvents) не должно подвисать на обработке, иначе тут будет подвисать consumer.
                                var triggers = await repository.LoadTriggerForQueueConsumerAsync(
                                    triggersGroups.Select(e => e.Key).ToArray(),
                                    cancellationToken);

                                foreach (var elem in triggersGroups)
                                {
                                    if (!triggers.TryGetValue(elem.Key, out var trigger))
                                    {
                                        // Тригер не найден или он завершен (фильтрует запрос).
                                        // Warning.
                                        continue;
                                    }

                                    // Перед этим его мог пытаться взять в обработку DbWorker, сбрасываем, чтобы убрать не нужную задержку.
                                    trigger.SelectLockTimeout = DateTimeOffset.MinValue;

                                    // Такого быть не может - фильтрует запрос.
                                    //if (trigger.IsCompleted)
                                    //{
                                    //    continue;
                                    //}

                                    // Событие с игнорированием текущей задержки.
                                    var haveIgnoreDelay = false;
                                    foreach (var elem2 in elem.Value)
                                    {
                                        haveIgnoreDelay = haveIgnoreDelay || elem2.IgnoreDelay;
                                    }
                                    
                                    if (haveIgnoreDelay)
                                    {
                                        trigger.TimerDate = DateTimeOffset.MinValue;
                                    }

                                    var eventsCount = elem.Value.Count();
                                    triggerSetter.OneOf(
                                        trigger,
                                        counterHandler: (counter) =>
                                        {
                                            triggerSetter.ProcessCounter(trigger, eventsCount);
                                            if (triggerSetter.IsCounterActivated(trigger))
                                            {
                                                triggerSetter.SetActivated(trigger, true);
                                            }
                                        },
                                        timerHandler: () => triggerSetter.SetActivated(trigger, true)
                                        );
                                }

                                await repository.SaveAsync(triggers.Values, cancellationToken);
                                await transaction.CommitAsync(cancellationToken);
                            }

                            await consumer.CommitAsync(cancellationToken);
                        }

                        if (executeOne)
                        {
                            break;
                        }
                    }
                }
                catch
                {
                    // log
                }
            }
        }

        public async Task DbWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken)
        {
            static async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> SelectAsync(
                IServiceProvider serviceProvider,
                SemaphoreSlim parallelLimiter,
                CancellationToken cancellationToken) 
            {
                var options = serviceProvider.GetRequiredService<Options>();
                var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                var factory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
                var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                var selectQuery = serviceProvider.GetRequiredService<ITriggerSelectQuery<TId>>();

                await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                {
                    return await selectQuery.SelectForProcessingAsync(
                        parallelLimiter.CurrentCount * 3,
                        options.DbExecuteSelectLockTimeout,
                        cancellationToken);
                }
            }

            static async Task ExecuteHandlerAsync(
                IServiceProvider serviceProvider,
                SemaphoreSlim parallelLimiter,
                ConcurrentDictionary<Guid, Task> tasks,
                ICollection<ITriggerSelectQuery<TId>.SelectDto> selectData,
                CancellationToken cancellationToken) 
            {
                static async Task ExecuteRangeHandlerAsync(
                    IServiceProvider serviceProvider,
                    IGrouping<string, ITriggerSelectQuery<TId>.SelectDto> group,
                    CancellationToken cancellationToken) 
                {
                    var options = serviceProvider.GetRequiredService<Options>();
                    var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                    var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                    var triggerSetter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();
                    var factory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();

                    var handler = (ITriggerRangeHandler<TId>)factory.GetHandler(serviceProvider, group.Key);

                    await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                    {
                        var triggers = await repository.LoadForHandlerAsync(
                            group.Select(e => e.Id).ToArray(),
                            waitLockTimeout: options.DbExecuteWaitTriggerLockTimeout,
                            cancellationToken);
                        if (!triggers.Any())
                        {
                            return;
                        }

                        var result = await handler.HandleAsync(triggers, cancellationToken);

                        foreach (var elem in triggers)
                        {
                            var elemResult = result[elem.Key];
                            WriteHandlerResult(triggerSetter, elem, elemResult);
                        }

                        await repository.SaveAsync(triggers, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                }

                static async Task ExecuteSinglehandlerAsync(
                    IServiceProvider serviceProvider,
                    ITriggerSelectQuery<TId>.SelectDto triggerInfo,
                    CancellationToken cancellationToken) 
                {
                    var options = serviceProvider.GetRequiredService<Options>();
                    var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                    var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                    var factory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
                    var triggerSetter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();

                    var handler = (ITriggerSingleHandler<TId>)factory.GetHandler(serviceProvider, triggerInfo.HandlerKey);

                    await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                    {
                        var trigger = (await repository.LoadForHandlerAsync(
                            [triggerInfo.Id],
                            waitLockTimeout: options.DbExecuteWaitTriggerLockTimeout,
                            cancellationToken))
                            .FirstOrDefault();
                        if (trigger is null)
                        {
                            return;
                        }

                        var result = await handler.HandleAsync(trigger, cancellationToken);
                        WriteHandlerResult(triggerSetter, trigger, result);

                        await repository.SaveAsync([trigger], cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                }

                var factory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
                // Группировка по ключу (Info: точка агрегации):
                // Если триггер групповой, то обработку будет одним батчем (одной транзакцией).
                var groupByHandler = selectData.GroupBy(e => e.HandlerKey);

                foreach (var group in groupByHandler)
                {
                    var handler = factory.GetHandler(serviceProvider, group.Key);

                    if (handler is ITriggerRangeHandler<TId> rangeHandler)
                    {
                        await parallelLimiter.WaitAsync();
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
                                            group,
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
                    else if (handler is ITriggerSingleHandler<TId> singleHandler)
                    {
                        foreach (var elem in group)
                        {
                            await parallelLimiter.WaitAsync();
                            var id = Guid.NewGuid();

                            var task = Task.Run(
                                async () =>
                                {
                                    try
                                    {
                                        await using (var scope = serviceProvider.CreateAsyncScope())
                                        {
                                            await ExecuteSinglehandlerAsync(
                                                scope.ServiceProvider,
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
                        // throw.
                    }
                }
            }

            var options = _serviceProvider.GetRequiredService<Options>();

            using var parallelLimiter = new SemaphoreSlim(options.DbExecuteParallelismLimit);
            var tasks = new ConcurrentDictionary<Guid, Task>();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;                    
                }

                try
                {
                    ICollection<ITriggerSelectQuery<TId>.SelectDto> selectData;
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        selectData = await SelectAsync(
                            scope.ServiceProvider,
                            parallelLimiter,
                            cancellationToken);
                    }

                    await ExecuteHandlerAsync(
                        _serviceProvider, 
                        parallelLimiter,
                        tasks,
                        selectData, 
                        cancellationToken);
                }
                catch
                {
                    // Log
                }

                if (executeOne)
                {
                    break;
                }
            }

            try
            {
                await Task.WhenAll(tasks.Values);
            }
            catch
            {
                // Log
            }
            tasks.Clear();
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static void WriteHandlerResult(
            ITriggerSetter<TId> setter,
            ITriggerComponent<TId> trigger,
            ITriggerHandler.Result result)
        {
            if (result.NeedRepeat)
            {
                setter.SetTimer(trigger, result.ExecuteDelay);
                setter.SetActivated(trigger, result.IsActivated);
                setter.SetCompleted(trigger, false);
            }
            else
            {
                setter.SetActivated(trigger, false);
                setter.SetCompleted(trigger, true);
            }
            // TODO: setter
            trigger.SelectLockTimeout = DateTimeOffset.MinValue;
        }


        public class Options
        {
            public int QueueConsumeBatchLimit { get; set; }
                = 200;

            public TimeSpan QueueConsumeBatchTimeout { get; set; }
                = TimeSpan.FromSeconds(1);

            public int DbExecuteParallelismLimit { get; set; }
                = 10;

            /// <summary>
            /// Select блокировка.
            /// </summary>
            public TimeSpan DbExecuteSelectLockTimeout { get; set; }
                = TimeSpan.FromSeconds(30);

            /// <summary>
            /// Время ожидания попытки получить блокировку на триггер.
            /// (Конкурецнтя с consumer).
            /// </summary>
            public TimeSpan DbExecuteWaitTriggerLockTimeout { get; set; }
                = TimeSpan.FromSeconds(5);
        }
    }    
}
