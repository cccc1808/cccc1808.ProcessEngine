using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.QueueModule;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerRunner<TId> : ITriggerRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsDto _options;


        public TriggerRunner(
            IServiceProvider serviceProvider, 
            OptionsDto options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public async Task ConsumerWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken)
        {
            static void EventTypeMismathError(ITriggerComponent<TId> trigger, ITriggerEvent triggerEvent)
            {
                // TODO: log error.
            }

            static async Task ConsumerHandlerAsync(
                IServiceProvider serviceProvider,
                Action<ITriggerComponent<TId>, ITriggerEvent> eventTypeMismathErrorHandler,
                QueueOptionsDto consumerQueueOptions,
                bool executeOne,                
                CancellationToken cancellationToken) 
            {
                var triggerOptions = serviceProvider.GetRequiredService<TriggerOptions<TId>>();
                var options = serviceProvider.GetRequiredService<OptionsDto>();
                var queueProviderFactory = serviceProvider.GetRequiredService<IQueueProviderFactory>();
                var serializer = serviceProvider.GetRequiredService<IEventJsonSerializer>();

                var receivedMessages = new LinkContainer<int>(0);
                var groupByTrigger = new Dictionary<string, List<ITriggerEvent>>();

                var consumer = await QueuePatternHelper.ConnectOrReconnectConsumerAsync(
                    queueProviderFactory,
                    options.ExceptionDelay,
                    consumer: null,
                    consumerQueueOptions.QueueName,
                    executeOne,
                    (ex) => {  /*TODO: log*/ },
                    cancellationToken);

                while (true)
                {
                    try
                    {
                        receivedMessages.Data = 0;
                        groupByTrigger.Clear();

                        await consumer.ConsumeBatchAsync(
                            (options, consumerQueueOptions, serializer, receivedMessages, groupByTrigger),
                            consumerQueueOptions.QueueConsumeBatchTimeout,
                            static (p, e) =>
                            {
                                // (Info: точка агрегации).
                                // N событий триггера сжимаются в одном действия (1 db update).
                                var triggerEvent = p.serializer.Deserialize(e.Body);
                                if (!p.groupByTrigger.TryGetValue(triggerEvent.TriggerKey, out var triggerEvents))
                                {
                                    // Если нужно, то тут можно сделать пулинг коллекций.
                                    triggerEvents = new List<ITriggerEvent>(p.consumerQueueOptions.QueueConsumeMessagesLimit / 2);
                                    p.groupByTrigger.Add(triggerEvent.TriggerKey, triggerEvents);
                                }
                                triggerEvents.Add(triggerEvent);
                                p.receivedMessages.Data++;

                                // Критерий лимита батча (помимо timeout) и количество сообщений и количество уникальных триггеров.
                                var stop =
                                    p.receivedMessages.Data >= p.consumerQueueOptions.QueueConsumeMessagesLimit
                                    || p.groupByTrigger.Count >= p.consumerQueueOptions.QueueConsumeTriggersCountLimit;
                                return !stop;
                            },
                            cancellationToken);

                        if (receivedMessages.Data == 0)
                        {
                            continue;
                        }

                        await using (var scope2 = serviceProvider.CreateAsyncScope())
                        {
                            try
                            {
                                await ProcessEventsHandlerAsync(
                                    scope2.ServiceProvider,
                                    eventTypeMismathErrorHandler,
                                    groupByTrigger,
                                    cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                // exception в хендлере.
                                if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                                {
                                    throw;
                                }

                                // TODO: log.

                                if (executeOne)
                                {
                                    throw;
                                }
                            }

                            await consumer.CommitAsync(cancellationToken);

                            if (executeOne)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // exception при работе с брокером.
                        if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                        {
                            throw;
                        }

                        // TODO: log.

                        if (executeOne)
                        {
                            throw;
                        }

                        consumer = await QueuePatternHelper.ConnectOrReconnectConsumerAsync(
                            queueProviderFactory,
                            options.ExceptionDelay,
                            consumer,
                            consumerQueueOptions.QueueName,
                            oneExecute: executeOne,
                            (ex) => {  /*TODO: log*/ },
                            cancellationToken);
                    }
                }
            }

            static async Task ProcessEventsHandlerAsync(
                IServiceProvider serviceProvider,
                Action<ITriggerComponent<TId>, ITriggerEvent> eventTypeMismathErrorHandler,
                Dictionary<string, List<ITriggerEvent>> groupByTrigger,
                CancellationToken cancellationToken)
            {
                var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
                var triggerSetter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();
                var rootTriggerService = serviceProvider.GetRequiredService<IRootTriggerService<TId>>();

                await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                {
                    // Триггеры (используемые для TriggerEvents) не должно подвисать на обработке (не содержат долгих операций), иначе тут будет подвисать consumer.
                    var triggers = await repository.LoadTriggerForQueueConsumerAsync(
                        groupByTrigger.Keys,
                        cancellationToken);

                    var now = dateTimeProvider.UtcNow;
                    var rootTriggersProcessGoSleep = new List<ITriggerComponent<TId>>(20);
                    
                    // 1) Обработка событий.
                    foreach (var elem in groupByTrigger)
                    {
                        if (!triggers.TryGetValue(elem.Key, out var trigger))
                        {
                            // Тригер не найден или он завершен (фильтрует запрос).
                            // TODO: log Warning.
                            continue;
                        }                        

                        // Такого быть не может - фильтрует запрос.
                        //if (trigger.IsCompleted)
                        //{
                        //    continue;
                        //}

                        triggerSetter.OneOfSetter.OneOfTrigger(
                            trigger,
                            (eventTypeMismathErrorHandler, triggerSetter, rootTriggersProcessGoSleep, trigger, now, messages: elem.Value),
                            counterHandler: static (state, p) =>
                            {
                                foreach (var elem in p.messages)
                                {
                                    p.triggerSetter.OneOfSetter.OneOfEvent(
                                        elem,
                                        (p.eventTypeMismathErrorHandler, p.triggerSetter, p.trigger, p.now, state),
                                        removeTriggerEventHandler: (_, p) => 
                                            p.triggerSetter.StandartSetter.ForRemove(p.trigger, true),
                                        counterTriggerEventHandler: static (typedEvent, p) =>                                        
                                            p.triggerSetter.CounterSetter.CounterEvent(
                                                p.trigger, 
                                                p.state, 
                                                typedEvent.Reset,
                                                typedEvent.Value),
                                        timerTriggerEventHandler: static (typedEvent, p) =>
                                            p.triggerSetter.StandartSetter.SetTimer(
                                                p.trigger,                                                
                                                new ITriggerSetter<TId>.IStandartSetter.TimerDto(
                                                    p.now,
                                                    typedEvent.Timer, typedEvent.IfDeltaMore                                                    
                                                    )                                                    
                                                ),
                                        signalSimpleStreamTriggerEventHandler: static  (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        processGoWaitStreamTriggerEventHandler: static  (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        processedOffsetTriggerEventHandler: static  (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        signalOffsetTriggerEventHandler: static  (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent)
                                        );
                                }

                                if (p.triggerSetter.CounterSetter.NeedActivate(p.trigger, state))
                                {
                                    p.triggerSetter.CounterSetter.Activate(p.trigger, state);
                                }
                            },
                            timerHandler: static (p) => 
                            {
                                foreach (var elem in p.messages)
                                {
                                    p.triggerSetter.OneOfSetter.OneOfEvent(
                                        elem,
                                        (p.eventTypeMismathErrorHandler, p.triggerSetter, p.trigger, p.now),
                                        removeTriggerEventHandler: (_, p) =>
                                            p.triggerSetter.StandartSetter.ForRemove(p.trigger, true),
                                        counterTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        timerTriggerEventHandler: static (typedEvent, p) =>
                                            p.triggerSetter.StandartSetter.SetTimer(
                                                p.trigger,
                                                new ITriggerSetter<TId>.IStandartSetter.TimerDto(
                                                    p.now,
                                                    typedEvent.Timer, typedEvent.IfDeltaMore
                                                    )
                                                ),
                                        signalSimpleStreamTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        processGoWaitStreamTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        processedOffsetTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        signalOffsetTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent)
                                        );
                                }
                            },
                            simpleStreamHandler: static (state, p) => 
                            {
                                foreach (var elem in p.messages)
                                {
                                    p.triggerSetter.OneOfSetter.OneOfEvent(
                                        elem,
                                        (p.eventTypeMismathErrorHandler, p.triggerSetter, p.trigger, state, p.now),
                                        removeTriggerEventHandler: (_, p) =>
                                            p.triggerSetter.StandartSetter.ForRemove(p.trigger, true),
                                        counterTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        timerTriggerEventHandler: static (typedEvent, p) =>
                                            p.triggerSetter.StandartSetter.SetTimer(
                                                p.trigger,
                                                new ITriggerSetter<TId>.IStandartSetter.TimerDto(
                                                    p.now,
                                                    typedEvent.Timer, typedEvent.IfDeltaMore
                                                    )
                                                ),
                                        signalSimpleStreamTriggerEventHandler: static (typedEvent, p) =>
                                            p.triggerSetter.SimpleStreamSetter.SignalEventReceived(p.trigger, p.state),
                                        processGoWaitStreamTriggerEventHandler: static (typedEvent, p) =>
                                            p.triggerSetter.SimpleStreamSetter.ProcessGoWaitEventReceived(p.trigger, p.state),                                        
                                        processedOffsetTriggerEventHandler: static  (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        signalOffsetTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent)
                                        );
                                }

                                if (p.triggerSetter.SimpleStreamSetter.NeedActivate(p.trigger, state))
                                {                                    
                                    p.triggerSetter.SimpleStreamSetter.Activate(p.trigger, state);
                                }
                                else 
                                {
                                    if (state.IsRootTrigger)
                                    {
                                        if (state.StreamsProcessIsWaiting)
                                        {
                                            // Не активирован (новых сигналов нет) и процесс засыпает.
                                            p.rootTriggersProcessGoSleep.Add(p.trigger);
                                        }
                                    }
                                }
                            },
                            offsetStreamHanler: (state, p) =>
                            {
                                foreach (var elem2 in elem.Value)
                                {
                                    p.triggerSetter.OneOfSetter.OneOfEvent(
                                        elem2,
                                        (p.eventTypeMismathErrorHandler, triggerSetter, trigger, state, p.now),
                                        removeTriggerEventHandler: (_, p) =>
                                            p.triggerSetter.StandartSetter.ForRemove(p.trigger, true),
                                        counterTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        timerTriggerEventHandler: static (typedEvent, p) =>
                                            p.triggerSetter.StandartSetter.SetTimer(
                                                p.trigger,
                                                new ITriggerSetter<TId>.IStandartSetter.TimerDto(
                                                    p.now,
                                                    typedEvent.Timer, typedEvent.IfDeltaMore
                                                    )
                                                ),                                        
                                        signalSimpleStreamTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        processGoWaitStreamTriggerEventHandler: static (typedEvent, p) =>
                                            p.triggerSetter.OffsetStreamSetter.ProcessGoWaitEventReceived(p.trigger, p.state),                                        
                                        processedOffsetTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            if (p.state.ProcessedOffset <= typedEvent.ProcessedOffset)
                                            {
                                                p.triggerSetter.OffsetStreamSetter.UpdateProcessedOffset(p.trigger, p.state, typedEvent.ProcessedOffset);
                                            }
                                            else
                                            {
                                                // TODO: log warnig событие с меньшим смещением.
                                            }
                                        },
                                        signalOffsetTriggerEventHandler: static (typedEvent, p) => 
                                        {
                                            if (p.state.LastOffset <= typedEvent.UpdateOffset)
                                            {
                                                p.triggerSetter.OffsetStreamSetter.UpdateLastOffset(p.trigger, p.state, typedEvent.UpdateOffset);
                                                p.state.LastOffset = typedEvent.UpdateOffset;
                                            }
                                            else
                                            {
                                                // TODO: log warnig событие с меньшим смещением.
                                            }
                                        }
                                        );
                                }
                                
                                if (p.triggerSetter.OffsetStreamSetter.NeedActivate(p.trigger, state))
                                {
                                    p.triggerSetter.OffsetStreamSetter.Activate(trigger, state);                                    
                                }
                            }
                            );

                        //if (
                        //    trigger.NeedUpdate
                        //    && trigger.IsActivated
                        //    && trigger.SelectLockTimeout > now)
                        //{
                        //    triggerSetter.StandartSetter.SetSelectLockTimeout(trigger, now);
                        //}
                    }

                    // 2) Оповещение дочерних триггеров о том, что процесс уснул.
                    await rootTriggerService.RootTriggerProcessGoSleepAsync(
                        rootTriggersProcessGoSleep, 
                        cancellationToken);

                    await repository.SaveAsync(triggers.Values, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
            }

            var runningTasks = new List<Task>(_options.TriggerEventQueues.Count);
            foreach (var elem in _options.TriggerEventQueues)
            {
                var t = Task.Run(
                    async () => 
                    {
                        await using (var scope = _serviceProvider.CreateAsyncScope())
                        {
                            await ConsumerHandlerAsync(
                                scope.ServiceProvider,
                                EventTypeMismathError,
                                elem, 
                                executeOne, 
                                cancellationToken);
                        }
                    });
                runningTasks.Add(t);
            }
            
            await Task.WhenAll(runningTasks);
        }

        public async Task DbWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken)
        {
            static async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> SelectAsync(
                IServiceProvider serviceProvider,
                SemaphoreSlim parallelLimiter,
                ITriggerSelectQuery<TId>.IContextState selectContext,
                CancellationToken cancellationToken) 
            {
                var options = serviceProvider.GetRequiredService<OptionsDto>();
                var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                var factory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
                var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                var selectQuery = serviceProvider.GetRequiredService<ITriggerSelectQuery<TId>>();

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
                SemaphoreSlim parallelLimiter,
                ConcurrentDictionary<Guid, Task> tasks,
                ICollection<ITriggerSelectQuery<TId>.SelectDto> selectData,
                CancellationToken cancellationToken) 
            {
                static async Task ExecuteRangeHandlerAsync(
                    IServiceProvider serviceProvider,
                    string handlerKey,
                    ITriggerSelectQuery<TId>.SelectDto[] group,
                    CancellationToken cancellationToken) 
                {
                    var options = serviceProvider.GetRequiredService<OptionsDto>();
                    var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
                    var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                    var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                    var triggerSetter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();
                    var factory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();

                    var handler = (ITriggerRangeHandler<TId>)factory.GetHandler(serviceProvider, handlerKey);

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
                            WriteHandlerResult(dateTimeProvider, triggerSetter, elem, elemResult);
                        }

                        // Тут учитывать сохранение triggerEntity, processEntity, wakeupEntity (Если не EF).
                        await repository.SaveAsync(triggers, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                }

                static async Task ExecuteSinglehandlerAsync(
                    IServiceProvider serviceProvider,
                    ITriggerSelectQuery<TId>.SelectDto triggerInfo,
                    CancellationToken cancellationToken) 
                {
                    var options = serviceProvider.GetRequiredService<OptionsDto>();
                    var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
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
                        WriteHandlerResult(dateTimeProvider, triggerSetter, trigger, result);

                        // Тут учитывать сохранение triggerEntity, processEntity, wakeupEntity (Если не EF).
                        await repository.SaveAsync([trigger], cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                }

                var options = serviceProvider.GetRequiredService<OptionsDto>();
                var factory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
                // Группировка по ключу (Info: точка агрегации):
                // Если триггер групповой, то обработку будет одним батчем (одной транзакцией).
                var groupByHandler = selectData.GroupBy(e => e.HandlerKey);

                foreach (var group in groupByHandler)
                {
                    var handler = factory.GetHandler(serviceProvider, group.Key);

                    if (handler is ITriggerRangeHandler<TId> rangeHandler)
                    {
                        foreach (var batch in group.Chunk(options.TransactionUpdateLimit))
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
                                                group.Key,
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

            var options = _serviceProvider.GetRequiredService<OptionsDto>();
            var TriggerSelectQuery = _serviceProvider.GetRequiredService<ITriggerSelectQuery<TId>>();

            using var parallelLimiter = new SemaphoreSlim(
                options.DbExecuteParallelismLimit 
                + 1 // на SelectAsync
                );
            var tasks = new ConcurrentDictionary<Guid, Task>();

            var selectContext = TriggerSelectQuery.BuildContext(options.SelectOptions);
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
                            selectContext,
                            cancellationToken);
                    }

                    if (!selectData.Any())
                    {
                        await Task.Delay(options.EmptySelectDelay, cancellationToken);
                        break;
                    }

                    await ExecuteHandlerAsync(
                        _serviceProvider, 
                        parallelLimiter,
                        tasks,
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

                    await Task.Delay(options.ExceptionDelay, cancellationToken);
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
            IDateTimeProvider dateTimeProvider,
            ITriggerSetter<TId> setter,
            ITriggerComponent<TId> trigger,
            ITriggerHandler.Result result)
        {
            if (result.NeedRepeat)
            {
                setter.StandartSetter.SetTimer(trigger, result.ExecuteDelay);
                setter.StandartSetter.SetActivated(trigger, result.IsActivated);
                setter.StandartSetter.SetCompleted(trigger, false);
            }
            else
            {
                setter.StandartSetter.SetActivated(trigger, false);
                setter.StandartSetter.SetCompleted(trigger, true);
            }
            setter.StandartSetter.SetSelectLockTimeout(trigger, dateTimeProvider.UtcNow);
        }


        public class OptionsDto
        {
            /// <summary>
            /// Конфигурация очередей, используемых для передачи <see cref="ITriggerEvent"/>, которые будут обрабатываться текущим экземпляром.
            /// (Можно сделать несколько одередей с разными значениями буфера и задержки накопления события).
            /// </summary>
            public List<QueueOptionsDto> TriggerEventQueues { get; set; }
                = new List<QueueOptionsDto>(0);                 

            public int DbExecuteParallelismLimit { get; set; }
                = 10;

            public ITriggerSelectQuery<TId>.IOptions SelectOptions { get; set; }

            /// <summary>
            /// Ограничение на количетсво триггеров, обновляемое в одной транзакции.
            /// <see cref="QueueOptionsDto.QueueConsumeTriggersCountLimit"/>.
            /// </summary>
            public int TransactionUpdateLimit { get; set; }
                = 100;

            /// <summary>
            /// Функция определения размера батча для выборки на обработку.
            /// Параметр - количество свободных слотов (DbExecuteParallelismLimit - RunningTasksCount).
            /// Если нода выполняет только долгие/<see cref="ITriggerSingleHandler{TId}"/>, то предпочтительнее (freeSlots) => freeSlots.
            /// </summary>
            public Func<int, int> DbExecuteBatchSize { get; set; }
                = static (freeSlots) => Math.Min(freeSlots * 50, 100);

            public TimeSpan EmptySelectDelay { get; set; } 
                = TimeSpan.FromMilliseconds(100);

            public TimeSpan ExceptionDelay { get; set; }
                = TimeSpan.FromSeconds(5);

            /// <summary>
            /// Блокировка, устанавливаемая на <see cref="ITriggerComponent{TId}.SelectLockTimeout"/>, чтобы другие ноды не натыкались на этот триггер 
            /// т.к. он зарезирвирован на выполнение текущей нодой (сбрасываеься при выполнении обработки).
            /// </summary>
            public TimeSpan DbExecuteSelectLockTimeout { get; set; }
                = TimeSpan.FromSeconds(30);

            /// <summary>
            /// Время ожидания попытки получить блокировку на триггер.
            /// (Конкурецнтя с consumer).
            /// </summary>
            public TimeSpan DbExecuteWaitTriggerLockTimeout { get; set; }
                = TimeSpan.FromSeconds(5);

            public OptionsDto(ITriggerSelectQuery<TId>.IOptions selectOptions)
            {
                SelectOptions = selectOptions;
            }
        }

        public class QueueOptionsDto 
        {
            public string QueueName { get; set; } 
                = null;

            /// <summary>
            /// Ограничение количества сообщений, закомиченных за один такт.
            /// </summary>
            public int QueueConsumeMessagesLimit { get; set; }
                = 1000;

            /// <summary>
            /// Ограничение количетва триггеров, обновленных за один такт (кол-во обновленных строк в БД).
            /// </summary>
            public int QueueConsumeTriggersCountLimit { get; set; }
                = 100;

            /// <summary>
            /// Ограничение задержки накопления батча событий.
            /// </summary>
            public TimeSpan QueueConsumeBatchTimeout { get; set; }
                = TimeSpan.FromSeconds(1);
        }
    }    
}
