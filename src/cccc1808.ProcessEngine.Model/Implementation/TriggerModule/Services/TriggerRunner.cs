using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        public TriggerRunner(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task ConsumerWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken)
        {
            static int EventTypeMismathError(ITriggerEvent triggerEvent, ITriggerComponent<TId> trigger)
            {
                // TODO: log error.
                return 1;
            }

            static async Task ProcessAsync(
                IServiceProvider serviceProvider,
                Dictionary<string, List<ITriggerEvent<TId>>> groupByTrigger,
                CancellationToken cancellationToken)
            {
                var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                var repository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
                var triggerSetter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();

                await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                {
                    // Триггеры (используемые для TriggerEvents) не должно подвисать на обработке (не содержат долгих операций), иначе тут будет подвисать consumer.
                    var triggers = await repository.LoadTriggerForQueueConsumerAsync(
                        groupByTrigger.Keys,
                        cancellationToken);

                    foreach (var elem in groupByTrigger)
                    {
                        if (!triggers.TryGetValue(elem.Key, out var trigger))
                        {
                            // Тригер не найден или он завершен (фильтрует запрос).
                            // TODO: log Warning.
                            continue;
                        }

                        // Перед этим его мог пытаться взять в обработку DbWorker, сбрасываем, чтобы убрать не нужную задержку.
                        trigger.SelectLockTimeout = DateTimeOffset.UtcNow;

                        // Такого быть не может - фильтрует запрос.
                        //if (trigger.IsCompleted)
                        //{
                        //    continue;
                        //}

                        triggerSetter.OneOfTrigger(
                            trigger,
                            (triggerSetter, trigger, messages: elem.Value),
                            counterHandler: static (state, p) =>
                            {
                                foreach (var elem in p.messages)
                                {
                                    p.triggerSetter.OneOfEvent(
                                        elem,
                                        (p.triggerSetter, p.trigger, state),                                        
                                        counterTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            if (typedEvent.Reset)
                                            {
                                                p.state.Counter = typedEvent.Value;
                                            }
                                            else
                                            {
                                                p.state.Counter += typedEvent.Value;
                                            }
                                            return 1;
                                        },
                                        timerTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            p.triggerSetter.SetTimer(p.trigger, typedEvent.Timer);
                                            return 1;
                                        },
                                        signalSimpleStreamTriggerEventHandler: (typedEvent, p) => 1,
                                        processGoWaitStreamTriggerEventHandler: (typedEvent, p) => 1,
                                        processedOffsetTriggerEventHandler: (typedEvent, p) => 1,
                                        signalOffsetTriggerEventHandler: (_, _) => 1
                                        );
                                }

                                if (state.Counter <= 0)
                                {
                                    p.triggerSetter.SetActivated(p.trigger, true);
                                }
                            },
                            timerHandler: static (p) => 
                            {
                                foreach (var elem in p.messages)
                                {
                                    p.triggerSetter.OneOfEvent(
                                        elem,
                                        (p.triggerSetter, p.trigger),
                                        counterTriggerEventHandler: static (typedEvent, p) => 1,
                                        timerTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            p.triggerSetter.SetTimer(p.trigger, typedEvent.Timer);
                                            return 1;
                                        },
                                        signalSimpleStreamTriggerEventHandler: static (typedEvent, p) => 1,
                                        processGoWaitStreamTriggerEventHandler: static (typedEvent, p) => 1,
                                        processedOffsetTriggerEventHandler: (typedEvent, p) => 1,
                                        signalOffsetTriggerEventHandler: (_, _) => 1
                                        );
                                }
                            },
                            simpleStreamHandler: static (state, p) => 
                            {
                                foreach (var elem in p.messages)
                                {
                                    p.triggerSetter.OneOfEvent(
                                        elem,
                                        (p.triggerSetter, p.trigger, state),
                                        counterTriggerEventHandler: static (_, _) => 1,
                                        timerTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            p.triggerSetter.SetTimer(p.trigger, typedEvent.Timer);
                                            return 1;
                                        },
                                        signalSimpleStreamTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            p.state.NewSignalCounter++;
                                            return 1;
                                        },
                                        processGoWaitStreamTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            p.state.StreamsProcessIsWaiting = true;
                                            return 1;
                                        },                                        
                                        processedOffsetTriggerEventHandler: static  (_, _) => 1,
                                        signalOffsetTriggerEventHandler: static (_, _) => 1
                                        );
                                }

                                if (state.NewSignalCounter != 0 && state.StreamsProcessIsWaiting)
                                {
                                    // Процесс на пробуждение, счетчик сбрасывается.
                                    p.triggerSetter.SetActivated(p.trigger, true);
                                    state.StreamsProcessIsWaiting = false;
                                    state.NewSignalCounter = 0;
                                }
                            },
                            offsetStreamHanler: (state, p) =>
                            {
                                foreach (var elem2 in elem.Value)
                                {
                                    p.triggerSetter.OneOfEvent(
                                        elem2,
                                        (triggerSetter, trigger, state),
                                        counterTriggerEventHandler: static (_, _) => 1,
                                        timerTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            p.triggerSetter.SetTimer(p.trigger, typedEvent.Timer);
                                            return 1;
                                        },                                        
                                        signalSimpleStreamTriggerEventHandler: static (_, _) => 1,
                                        processGoWaitStreamTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            p.state.StreamsProcessIsWaiting = true;
                                            return 1;
                                        },                                        
                                        processedOffsetTriggerEventHandler: static (typedEvent, p) =>
                                        {
                                            if (p.state.ProcessedOffset < typedEvent.ProcessedOffset)
                                            {
                                                p.state.ProcessedOffset = typedEvent.ProcessedOffset;
                                            }
                                            else
                                            {
                                                // TODO: log warnig событие с меньшим смещением.
                                            }

                                            return 1;
                                        },
                                        signalOffsetTriggerEventHandler: static (typedEvent, p) => 
                                        {
                                            if (p.state.LastOffset < typedEvent.UpdateOffset)
                                            {
                                                p.state.LastOffset = typedEvent.UpdateOffset;
                                            }
                                            else
                                            {
                                                // TODO: log warnig событие с меньшим смещением.
                                            }

                                            return 1;
                                        }
                                        );
                                }

                                // Не все смещенеи обрботано.
                                var haveNotProcessedSignals = state.ProcessedOffset < state.LastOffset;

                                // Если процесс уснул и не все смещенеи обрботано.
                                if (state.StreamsProcessIsWaiting && haveNotProcessedSignals)
                                {
                                    // Взводим триггер на пробуждение.
                                    triggerSetter.SetActivated(trigger, true);
                                    state.StreamsProcessIsWaiting = false;
                                }
                            }
                            );
                    }
                    
                    await repository.SaveAsync(triggers.Values, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
            }

            await using (var scope = _serviceProvider.CreateAsyncScope())
            {
                var triggerOptions = scope.ServiceProvider.GetRequiredService<TriggerOptions<TId>>();
                var options = scope.ServiceProvider.GetRequiredService<OptionsDto>();
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var serializer = scope.ServiceProvider.GetRequiredService<IEventJsonSerializer<TId>>();

                var receivedMessages = new LinkContainer<int>(0);
                var groupByTrigger = new Dictionary<string, List<ITriggerEvent<TId>>>();

                var consumer = await QueuePatternHelper.ConnectOrReconnectConsumerAsync(
                    queueProviderFactory,
                    options.ExceptionDelay,
                    consumer: null,
                    triggerOptions.TriggerEventQueueName,
                    executeOne,
                    (ex) => {  /*TODO: log*/ },
                    cancellationToken);
                
                while (true)
                {
                    try
                    {
                        receivedMessages.Data = 0;
                        groupByTrigger.Clear();

                        // TODO: не было бы лишним допускать использование нескольких топиков (потребителей),
                        // * например для inbox большое значение QueueConsumeBatchTimeout/QueueConsumePackTimeout не очень подходит,
                        // * а для parent-child процесса (с большим количеством дочерних) QueueConsumeBatchTimeout/QueueConsumePackTimeout может быть больше (чтобы снизить нагрузку записи на БД).
                        await consumer.ConsumeBatchAsync(
                            (options, serializer, receivedMessages, groupByTrigger),
                            options.QueueConsumePackTimeout,
                            options.QueueConsumePackSize,
                            options.QueueConsumeBatchTimeout,
                            static (p, e) =>
                            {
                                if (!e.Any())
                                {
                                    return true;
                                }

                                // (Info: точка агрегации).
                                // N событий триггера сжимаются в одном действия (1 db update).
                                foreach (var elem in e)
                                {
                                    var triggerEvent = p.serializer.Deserialize(elem.Body);

                                    if (!p.groupByTrigger.TryGetValue(triggerEvent.TriggerKey, out var triggerEvents))
                                    {
                                        triggerEvents = new List<ITriggerEvent<TId>>(e.Count);
                                        p.groupByTrigger.Add(triggerEvent.TriggerKey, triggerEvents);
                                    }
                                    triggerEvents.Add(triggerEvent);
                                }
                                p.receivedMessages.Data += e.Count;

                                // Критерий лимита батча (помимо timeout) и количество сообщений и количество уникальных триггеров.
                                var stop =
                                    p.receivedMessages.Data > p.options.QueueConsumeMessagesLimit
                                    || p.groupByTrigger.Count > p.options.QueueConsumeTriggersCountLimit;
                                return !stop;
                            },
                            cancellationToken);

                        if (receivedMessages.Data == 0)
                        {
                            continue;
                        }

                        await using (var scope2 = scope.ServiceProvider.CreateAsyncScope())
                        {
                            try
                            {
                                await ProcessAsync(
                                    scope2.ServiceProvider,
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
                            triggerOptions.TriggerEventQueueName,
                            oneExecute: false,
                            (ex) => {  /*TODO: log*/ },
                            cancellationToken);
                    }
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
                var options = serviceProvider.GetRequiredService<OptionsDto>();
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
                    var options = serviceProvider.GetRequiredService<OptionsDto>();
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

                        // Тут учитывать сохранение triggerEntity, processEntity, wakeupEntity (Если не EF).
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

            var options = _serviceProvider.GetRequiredService<OptionsDto>();

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


        public class OptionsDto
        {
            public int QueueConsumePackSize { get; set; }
                = 200;

            /// <summary>
            /// Ограничение количества сообщений, закомиченных за один такт.
            /// </summary>
            public int QueueConsumeMessagesLimit { get; set; }
                = 1000;

            /// <summary>
            /// Ограничение количетва триггеров, обновленных за один такт (кол-во обновленных строк в БД).
            /// </summary>
            public int QueueConsumeTriggersCountLimit { get; set; }
                = 200;

            public TimeSpan QueueConsumePackTimeout { get; set; }
                = TimeSpan.FromMilliseconds(200);

            public TimeSpan QueueConsumeBatchTimeout { get; set; }
                = TimeSpan.FromSeconds(1);

            public int DbExecuteParallelismLimit { get; set; }
                = 10;

            public TimeSpan ExceptionDelay { get; set; }

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
