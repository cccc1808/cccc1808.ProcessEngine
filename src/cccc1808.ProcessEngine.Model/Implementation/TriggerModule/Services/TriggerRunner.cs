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
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.QueueModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerRunner<TId> 
        : ITriggerRunner
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
                var recheckProcessStatusBuffer = new Dictionary<string, bool>();

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
                        recheckProcessStatusBuffer.Clear();

                        await consumer.ConsumeBatchAsync(
                            (options, consumerQueueOptions, serializer, receivedMessages, groupByTrigger, recheckProcessStatusBuffer),
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

                                if (triggerEvent.Kind == TriggerEventKindEnum.RecheckProcessStatusStreamTriggerEvent)
                                {
                                    p.recheckProcessStatusBuffer.Add(triggerEvent.TriggerKey, false);
                                }

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
                                    recheckProcessStatusBuffer,
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
                Dictionary<string, bool> recheckProcessStatusBuffer,
                CancellationToken cancellationToken)
            {
                var options = serviceProvider.GetRequiredService<OptionsDto>();
                var emergencyOptions = serviceProvider.GetRequiredService<EmergencyTriggerHandler<TId>.OptionsDto>();
                var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
                var triggerSetter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();
                var triggerEventRaiser = serviceProvider.GetRequiredService<ITriggerEventRaiser<TId>>();
                var triggerQueueContext = serviceProvider.GetRequiredService<ITriggerQueueContext<TId>>();
                var triggerHandlerFactory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
                var condition = serviceProvider.GetRequiredService<ITriggerComponentCondition<TId>>();

                await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                {
                    // Триггеры (используемые для TriggerEvents) не должно подвисать на обработке (не содержат долгих операций), иначе тут будет подвисать consumer.
                    var triggers = await repository.LoadTriggerForQueueConsumerAsync(
                        groupByTrigger.Keys,
                        cancellationToken);

                    triggerQueueContext.IncreseBufferCapacity(triggers.Count);
                    triggerQueueContext.SetReserveTimeout(options.DbSelect_RangeReservationTimeout);

                    // Для stream триггера треюуется перепроверить статус процесса.
                    if (recheckProcessStatusBuffer.Any())
                    {
                        var processIds = recheckProcessStatusBuffer
                            .Select(e => triggers[e.Key].ProcessId)
                            .ToArray();

                        // [MVCC Only]: подумать если иначе.
                        // TODO: проверяе что процесс спит.
                        var checkResult = await repository.CheckProcessWaitingAsync(processIds, cancellationToken);
                        foreach (var elem in triggers.Values)
                        {
                            if (checkResult.Contains(elem.ProcessId))
                            {
                                recheckProcessStatusBuffer[elem.Key] = true;
                            }
                        }
                    }

                    var now = dateTimeProvider.UtcNow;
                    var sendEventsBuffer = new List<ITriggerEventRaiser<TId>.RaiseContainer>(triggers.Count);
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

                        triggerSetter.OneOfTriggerSetter.OneOf(
                            trigger,
                            (
                                eventTypeMismathErrorHandler, 
                                triggerSetter,
                                triggerQueueContext,
                                trigger,
                                emergencyOptions,
                                now,
                                messages: elem.Value, 
                                recheckProcessStatusBuffer, 
                                sendEventsBuffer
                                ),
                            // 1) counter
                            counterHandler: static (state, p) =>
                            {
                                foreach (var elem in p.messages)
                                {
                                    p.triggerSetter.OneOfTriggerEventSetter.OneOf(
                                        elem,
                                        (p.eventTypeMismathErrorHandler, p.triggerSetter, p.triggerQueueContext, p.trigger, p.now, state),
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
                                        signalSimpleStreamTriggerEventHandler: static (typedEvent, p) =>
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        processGoWaitStreamTriggerEventHandler: static (typedEvent, p) =>
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        processedOffsetTriggerEventHandler: static (typedEvent, p) =>
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        signalOffsetTriggerEventHandler: static (typedEvent, p) =>
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        recheckProcessStatusStreamTriggerEventHandler: static (typedEvent, p) =>
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        deliveryResultEventHandler: static (typedEvent, p) =>
                                        {
                                            if (p.triggerSetter.ChildTriggerSetter.IsChildTrigger(p.trigger, out var childState))
                                            {
                                                p.triggerSetter.ChildTriggerSetter.DeliveryResultReceived(p.trigger, childState, typedEvent.Timestamp);
                                            }
                                            else
                                            {
                                                p.eventTypeMismathErrorHandler(p.trigger, typedEvent);
                                            }
                                        }
                                        );
                                }

                                if (p.triggerSetter.CounterSetter.NeedActivate(p.trigger, state))
                                {
                                    p.triggerSetter.CounterSetter.Activate(p.trigger, state);
                                }
                            },
                            // 2) timer
                            timerHandler: static (p) => 
                            {
                                foreach (var elem in p.messages)
                                {
                                    p.triggerSetter.OneOfTriggerEventSetter.OneOf(
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
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        recheckProcessStatusStreamTriggerEventHandler: static (typedEvent, p) =>
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        deliveryResultEventHandler: static (typedEvent, p) =>
                                        {
                                            if (p.triggerSetter.ChildTriggerSetter.IsChildTrigger(p.trigger, out var childState))
                                            {
                                                p.triggerSetter.ChildTriggerSetter.DeliveryResultReceived(p.trigger, childState, typedEvent.Timestamp);
                                            }
                                            else
                                            {
                                                p.eventTypeMismathErrorHandler(p.trigger, typedEvent);
                                            }
                                        }
                                        );
                                }
                            },
                            // 3) simpleStream
                            simpleStreamHandler: static (state, p) => 
                            {
                                foreach (var elem in p.messages)
                                {
                                    p.triggerSetter.OneOfTriggerEventSetter.OneOf(
                                        elem,
                                        (p.eventTypeMismathErrorHandler, p.triggerSetter, p.trigger, p.emergencyOptions, state, p.now, p.recheckProcessStatusBuffer, p.sendEventsBuffer),
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
                                        {
                                            p.triggerSetter.SimpleStreamSetter.SignalEventReceived(p.trigger, p.state);

                                            if (p.state.IsRootTrigger && typedEvent.SendTriggerKey != null)
                                            {
                                                // Если это корневой триггер, то посылаем подтверждение о получении сигнала для дочернего триггера.
                                                p.sendEventsBuffer.Add(
                                                    new ITriggerEventRaiser<TId>.RaiseContainer(
                                                    p.emergencyOptions.TriggerEventQueue,
                                                    p.trigger.ProcessId,
                                                    new DeliveryResultEvent(
                                                        triggerKey: typedEvent.SendTriggerKey, 
                                                        timestamp: typedEvent.SendTimeStamp.Value!
                                                        )
                                                    ));
                                            }
                                        },
                                        processGoWaitStreamTriggerEventHandler: static (typedEvent, p) =>
                                            p.triggerSetter.SimpleStreamSetter.ProcessGoWaitEventReceived(p.trigger, p.state),                                        
                                        processedOffsetTriggerEventHandler: static  (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        signalOffsetTriggerEventHandler: static (typedEvent, p) => 
                                            p.eventTypeMismathErrorHandler(p.trigger, typedEvent),
                                        recheckProcessStatusStreamTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            // Emergency trigger сообщил, что процесс возможно спит, а strean триггер не знает об этом (потеря события).
                                            if (
                                                !p.trigger.IsActivated 
                                                && !p.state.StreamsProcessIsWaiting 
                                                && p.recheckProcessStatusBuffer.TryGetValue(p.trigger.Key, out var processIsWaiting)
                                                && processIsWaiting)
                                            {
                                                p.triggerSetter.SimpleStreamSetter.ProcessGoWaitEventReceived(
                                                    p.trigger, 
                                                    p.state);
                                            }
                                        },
                                        deliveryResultEventHandler: static (typedEvent, p) =>
                                        {
                                            if (p.triggerSetter.ChildTriggerSetter.IsChildTrigger(p.trigger, out var childState))
                                            {
                                                p.triggerSetter.ChildTriggerSetter.DeliveryResultReceived(p.trigger, childState, typedEvent.Timestamp);
                                            }
                                            else
                                            {
                                                p.eventTypeMismathErrorHandler(p.trigger, typedEvent);
                                            }
                                        }
                                        );
                                }

                                if (p.triggerSetter.SimpleStreamSetter.NeedActivate(p.trigger, state))
                                {                                    
                                    p.triggerSetter.SimpleStreamSetter.Activate(p.trigger, state);
                                }
                            },
                            // 4) offsetStream
                            offsetStreamHanler: (state, p) =>
                            {
                                foreach (var elem2 in elem.Value)
                                {
                                    p.triggerSetter.OneOfTriggerEventSetter.OneOf(
                                        elem2,
                                        (p.eventTypeMismathErrorHandler, triggerSetter, trigger, state, p.now, p.recheckProcessStatusBuffer),
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
                                        },
                                        recheckProcessStatusStreamTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            // Emergency trigger сообщил, что процесс возможно спит, а strean триггер не знает об этом (потеря события).
                                            if (
                                                !p.trigger.IsActivated
                                                && !p.state.StreamsProcessIsWaiting
                                                && p.recheckProcessStatusBuffer.TryGetValue(p.trigger.Key, out var processIsWaiting)
                                                && processIsWaiting)
                                            {
                                                p.triggerSetter.OffsetStreamSetter.ProcessGoWaitEventReceived(
                                                    p.trigger,
                                                    p.state);
                                            }
                                        },
                                        deliveryResultEventHandler: static (typedEvent, p) =>
                                        {
                                            if (p.triggerSetter.ChildTriggerSetter.IsChildTrigger(p.trigger, out var childState))
                                            {
                                                p.triggerSetter.ChildTriggerSetter.DeliveryResultReceived(p.trigger, childState, typedEvent.Timestamp);
                                            }
                                            else
                                            {
                                                p.eventTypeMismathErrorHandler(p.trigger, typedEvent);
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

                        if ((trigger.ReservationTimeout - now) >= emergencyOptions.LostTriggerTimeout)
                        {
                            // Обновляем select lock timeout, чтобы обозначить, что на триггер поступают события.
                            triggerSetter.StandartSetter.SetReservationTimeout(trigger, now);
                        }

                        if (condition.NeedExecuteCondition.Check(
                                trigger,
                                new ITriggerComponentCondition<TId>.NeedExecuteParameters(
                                    dateTimeProvider.UtcNow)))
                        {
                            triggerQueueContext.TriggerContinueExecute(
                                ITriggerQueueContext<TId>.TriggerDto.TriggerContinueRun(
                                    trigger.Id,
                                    triggerHandlerFactory.IsRangeHandler(serviceProvider, trigger.HandlerKey),
                                    trigger.HandlerKey));
                        }

                        //if (
                        //    trigger.NeedUpdate
                        //    && trigger.IsActivated
                        //    && trigger.SelectLockTimeout > now)
                        //{
                        //    triggerSetter.StandartSetter.SetSelectLockTimeout(trigger, now);
                        //}
                    }

                    if (sendEventsBuffer.Any())
                    {
                        await triggerEventRaiser.RaiseAsync(
                            sendEventsBuffer, 
                            cancellationToken);
                    }

                    await repository.SaveAsync(triggers.Values, cancellationToken);                    

                    await transaction.CommitAsync(cancellationToken);
                }
            }

            var runningTasks = new List<Task>(_options.Consumer_TriggerEventQueues.Count);
            foreach (var elem in _options.Consumer_TriggerEventQueues)
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

        public async Task DbSelectorAsync(bool executeOne, CancellationToken cancellationToken)
        {
            var triggerRegistry = _serviceProvider.GetRequiredService<ITriggerRegistry>();            

            var parallelLimit = new SemaphoreSlim(_options.DbSelect_ParallilLimit);

            var executeOneTriggered = false;
            var tasks = new ConcurrentDictionary<Guid, Task>();

            foreach (var elem in triggerRegistry.GetAll())
            {
                bool isRange;
                await using (var s = _serviceProvider.CreateAsyncScope())
                {
                    isRange = s.ServiceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>()
                        .IsRangeHandler(s.ServiceProvider, elem.HandlerName);
                }

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
                                        var query = scope.ServiceProvider.GetRequiredService<ITriggerSelectQuery<TId>>();
                                        var triggerQueueContext = scope.ServiceProvider.GetRequiredService<ITriggerQueueContext<TId>>();

                                        var context = query.InitContext(_options.DbSelect_Options, elem.HandlerName);

                                        while (true)
                                        {
                                            var selectData = await query.ExecuteAsync(context, cancellationToken);

                                            if (!selectData.Any())
                                            {
                                                timeout = options.DbSelect_EmptyDelay;
                                                break;
                                            }

                                            var queueIsFull = await triggerQueueContext.TriggerFromSelector(
                                                selectData
                                                    .Select(e => ITriggerQueueContext<TId>.TriggerDto.TriggerFromSelector(
                                                        e.TriggerId,
                                                        isRange,
                                                        e.HandlerKey)
                                                    )
                                                    .ToArray(),
                                                reserveDate: isRange
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
                        catch(Exception ex)
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

        public async Task RangeTriggerProcessingAsync(bool executeOne, CancellationToken cancellationToken)
        {
            static async Task<ICollection<ITriggerQueueProvider<TId>.MessageDto>> ConsumeAsync(
                IServiceProvider serviceProvider,
                SemaphoreSlim parallelLimiter,
                CancellationToken cancellationToken)
            {
                var options = serviceProvider.GetRequiredService<OptionsDto>();
                var triggerQueue = serviceProvider.GetRequiredService<ITriggerQueueProvider<TId>>();

                // Ждем освобождения хотя бы одного слота.
                await parallelLimiter.WaitAsync(cancellationToken);
                try
                {
                    var freeSlots = parallelLimiter.CurrentCount;

                    var messages = await triggerQueue.ConsumeRangeTriggersAsync(
                        batchLimit: freeSlots * options.TransactionUpdateLimit,
                        uniqueHandlersLimit: freeSlots,
                        options.RangeExecutor_ConsumeTimeout,
                        cancellationToken);

                    return messages;
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
                ICollection<ITriggerQueueProvider<TId>.MessageDto> selectData,
                CancellationToken cancellationToken)
            {
                static async Task ExecuteRangeHandlerAsync(
                    IServiceProvider serviceProvider,
                    string handlerKey,
                    ITriggerQueueProvider<TId>.MessageDto[] group,
                    CancellationToken cancellationToken)
                {
                    var options = serviceProvider.GetRequiredService<OptionsDto>();
                    var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
                    var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                    var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                    var triggerSetter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();
                    var factory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
                    var triggerQueueContext = serviceProvider.GetRequiredService<ITriggerQueueContext<TId>>();
                    var condition = serviceProvider.GetRequiredService<ITriggerComponentCondition<TId>>();

                    var handler = (ITriggerRangeHandler<TId>)factory.GetHandler(serviceProvider, handlerKey);

                    await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                    {
                        var triggers = await repository.LoadForHandlerAsync(
                            group.Select(e => e.TriggerId).ToArray(),
                            waitLockTimeout: options.Executor_WaitTriggerLockTimeout,
                            cancellationToken);
                        if (!triggers.Any())
                        {
                            return;
                        }

                        triggerQueueContext.IncreseBufferCapacity(triggers.Count);
                        triggerQueueContext.SetReserveTimeout(options.DbSelect_RangeReservationTimeout);

                        var result = await handler.CheckAsync(
                            triggers,
                            isEmergencyTrigger: false,
                            cancellationToken);

                        var now = dateTimeProvider.UtcNow;
                        var forExecute = new List<ITriggerComponent<TId>>(result.Count);
                        foreach (var elem in triggers)
                        {
                            var elemResult = result[elem.Key];

                            if (!triggerSetter.ChildTriggerSetter.IsChildTrigger(elem, out var childTriggerState))
                            {
                                triggerSetter.StandartSetter.SetTriggerResult(elem, elemResult.Result);
                            }
                            else
                            {
                                triggerSetter.ChildTriggerSetter.SetTriggerResult(
                                    elem,
                                    childTriggerState,
                                    elemResult.Result,
                                    triggerSetter.ChildTriggerSetter.DateToTimestamp(now));
                            }

                            if (condition.NeedExecuteCondition.Check(
                                elem, 
                                new ITriggerComponentCondition<TId>.NeedExecuteParameters(
                                    dateTimeProvider.UtcNow)))
                            {
                                triggerQueueContext.TriggerContinueExecute(
                                    ITriggerQueueContext<TId>.TriggerDto.TriggerContinueRun(
                                        elem.Id,
                                        IsRangeTrigger: true, 
                                        elem.HandlerKey));
                            }
                            else 
                            {
                                triggerQueueContext.TriggerExecuted(elem.Id);
                            }

                            if (elemResult.NeedExecute)
                            {
                                forExecute.Add(elem);
                            }
                        }

                        await handler.ExecuteAsync(
                            forExecute,
                            cancellationToken
                            );

                        // Тут учитывать сохранение triggerEntity, processEntity (Если не EF).
                        await repository.SaveAsync(triggers, cancellationToken);                        

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
                                                scope.ServiceProvider,
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
                    else
                    {
                        // throw.
                    }
                }
            }

            var options = _serviceProvider.GetRequiredService<OptionsDto>();

            using var parallelLimiter = new SemaphoreSlim(
                options.RangeExecutor_ExecuteParallelismLimit 
                + 1 // На ожидание consumer на освобождение слота
                );
            var tasks = new ConcurrentDictionary<Guid, Task>();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    ICollection<ITriggerQueueProvider<TId>.MessageDto> selectData;
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        selectData = await ConsumeAsync(
                            scope.ServiceProvider,
                            parallelLimiter,
                            cancellationToken);
                    }

                    if (!selectData.Any())
                    {
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

        public async Task SignleTriggerProcessingAsync(bool executeOne, CancellationToken cancellationToken)
        {
            static async Task<ICollection<ITriggerQueueProvider<TId>.MessageDto>> ConsumeAsync(
                IServiceProvider serviceProvider,
                SemaphoreSlim parallelLimiter,
                CancellationToken cancellationToken)
            {
                var options = serviceProvider.GetRequiredService<OptionsDto>();

                var triggerQueue = serviceProvider.GetRequiredService<ITriggerQueueProvider<TId>>();

                // Ждем освобождения хотя бы одного слота.
                await parallelLimiter.WaitAsync(cancellationToken);
                try
                {
                    var messages = await triggerQueue.ConsumeSignleTriggersAsync(
                        parallelLimiter.CurrentCount,
                        options.SingleExecutor_ConsumeTimeout,
                        cancellationToken);
                    return messages;
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
                ICollection<ITriggerQueueProvider<TId>.MessageDto> selectData,
                CancellationToken cancellationToken)
            {
                static async Task ExecuteSinglehandlerAsync(
                    IServiceProvider serviceProvider,
                    ITriggerQueueProvider<TId>.MessageDto triggerInfo,
                    CancellationToken cancellationToken)
                {
                    var options = serviceProvider.GetRequiredService<OptionsDto>();
                    var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
                    var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                    var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                    var triggerQueueContext = serviceProvider.GetRequiredService<ITriggerQueueContext<TId>>();
                    var factory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
                    var triggerSetter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();
                    var condition = serviceProvider.GetRequiredService<ITriggerComponentCondition<TId>>();

                    var handler = (ITriggerSingleHandler<TId>)factory.GetHandler(serviceProvider, triggerInfo.HandlerKey);

                    await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                    {
                        var trigger = (await repository.LoadForHandlerAsync(
                            [triggerInfo.TriggerId],
                            waitLockTimeout: options.Executor_WaitTriggerLockTimeout,
                            cancellationToken))
                            .FirstOrDefault();
                        if (trigger is null)
                        {
                            return;
                        }

                        triggerQueueContext.IncreseBufferCapacity(1);
                        triggerQueueContext.SetReserveTimeout(options.SingleExecutor_ConsumeTimeout);

                        var result = await handler.HandleAsync(trigger, cancellationToken);
                        triggerSetter.StandartSetter.SetTriggerResult(trigger, result);

                        if (condition.NeedExecuteCondition.Check(
                                trigger,
                                new ITriggerComponentCondition<TId>.NeedExecuteParameters(
                                    dateTimeProvider.UtcNow)))
                        {
                            triggerQueueContext.TriggerContinueExecute(
                                ITriggerQueueContext<TId>.TriggerDto.TriggerContinueRun(
                                    trigger.Id,
                                    IsRangeTrigger: false,
                                    HandlerKey: trigger.HandlerKey));
                        }
                        else
                        {
                            triggerQueueContext.TriggerExecuted(trigger.Id);
                        }
                        
                        // Тут учитывать сохранение triggerEntity, processEntity (Если не EF).
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
                    
                    if (handler is ITriggerSingleHandler<TId> singleHandler)
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

            using var parallelLimiter = new SemaphoreSlim(
                options.SingleExecutor_ParallelismLimit
                + 1 // На ожидание consumer на освобождение слота
                );
            var tasks = new ConcurrentDictionary<Guid, Task>();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    ICollection<ITriggerQueueProvider<TId>.MessageDto> selectData;
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        selectData = await ConsumeAsync(
                            scope.ServiceProvider,
                            parallelLimiter,
                            cancellationToken);
                    }

                    if (!selectData.Any())
                    {
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

        public class OptionsDto
        {
            /// <summary>
            /// Конфигурация очередей, используемых для передачи <see cref="ITriggerEvent"/>, которые будут обрабатываться текущим экземпляром.
            /// (Можно сделать несколько одередей с разными значениями буфера и задержки накопления события).
            /// </summary>
            public List<QueueOptionsDto> Consumer_TriggerEventQueues { get; set; }
                = new List<QueueOptionsDto>(0);

            public int RangeExecutor_ExecuteParallelismLimit { get; set; }
                = 20;

            public TimeSpan RangeExecutor_ConsumeTimeout { get; set; }
                = TimeSpan.FromSeconds(0.1);

            public int SingleExecutor_ParallelismLimit { get; set; }
                = 3;

            public TimeSpan SingleExecutor_ConsumeTimeout { get; set; }
                = TimeSpan.FromSeconds(0.5);

            public required ITriggerSelectQuery<TId>.IOptions DbSelect_Options { get; set; }

            /// <summary>
            /// Ограничение на количетсво триггеров, обновляемое в одной транзакции.
            /// <see cref="QueueOptionsDto.QueueConsumeTriggersCountLimit"/>.
            /// </summary>
            public int TransactionUpdateLimit { get; set; }
                = 50;

            public TimeSpan ExceptionDelay { get; set; }
                = TimeSpan.FromSeconds(5);

            /// <summary>
            /// Время ожидания попытки получить блокировку на триггер.
            /// (Конкурецнтя с consumer).
            /// </summary>
            public TimeSpan Executor_WaitTriggerLockTimeout { get; set; }
                = TimeSpan.FromSeconds(5);

            public int DbSelect_ParallilLimit { get; set; }
                = 2;

            public TimeSpan DbSelect_EmptyDelay { get; set; }
                = TimeSpan.FromSeconds(1);

            public TimeSpan DbSelect_QueueIsFullTimeout { get; set; }
                = TimeSpan.FromSeconds(3);

            public TimeSpan DbSelect_RangeReservationTimeout { get; set; }
                = TimeSpan.FromSeconds(10);

            public TimeSpan DbSelect_SingleReservationTimeout { get; set; }
                = TimeSpan.FromSeconds(60);
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
