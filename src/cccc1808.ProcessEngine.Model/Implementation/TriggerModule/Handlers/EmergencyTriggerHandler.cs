using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers
{
    /// <summary>
    /// Универсальный страхующий триггер.
    /// Проверяет все не корневые range триггеры, которые давно не выполнялись.
    /// </summary>
    public class EmergencyTriggerHandler<TId>
        : ITriggerSingleHandler<TId>
    {
        public static string Name
            => nameof(EmergencyTriggerHandler<TId>);

        private readonly IServiceProvider _serviceProvider;
        private readonly IDateTimeProvider _dateTimeProvider;

        private readonly OptionsDto _options;

        public EmergencyTriggerHandler(
            IServiceProvider serviceProvider,
            IDateTimeProvider dateTimeProvider, 

            OptionsDto options)
        {
            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;

            _options = options;
        }

        public async ValueTask<ITriggerHandler.ResultDto> HandleAsync(
            ITriggerComponent<TId> trigger, 
            CancellationToken cancellationToken)
        {
            var softTimeout = _dateTimeProvider.UtcNow + _options.SoftTimeout;            

            while (true)
            {
                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    var result = await ExecuteAsync(
                        scope.ServiceProvider,
                        trigger,
                        cancellationToken);

                    if (!result)
                    {
                        break;
                    }
                }

                if (_dateTimeProvider.UtcNow >= softTimeout)
                {
                    return ITriggerHandler.ResultDto.ActivateResult();
                }
            }

            return ITriggerHandler.ResultDto.ActivateResult(
                _dateTimeProvider.UtcNow + _options.LostTriggerTimeout);
        }

        private static async Task<bool> ExecuteAsync(
            IServiceProvider serviceProvider,
            ITriggerComponent<TId> emergencyTrigger,
            CancellationToken cancellationToken)
        {
            var options = serviceProvider.GetRequiredService<OptionsDto>();
            var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
            var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
            var query = serviceProvider.GetRequiredService<IQueries>();
            var processRegistry = serviceProvider.GetRequiredService<IProcessRegistry>();
            var triggerHandlerFactory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
            var triggerRepository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
            var triggerEventRaiser = serviceProvider.GetRequiredService<ITriggerEventRaiser<TId>>();
            var setter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();
            var rootTriggerQuery = serviceProvider.GetRequiredService<IRootTriggerQuery<TId>>();

            var now = dateTimeProvider.UtcNow;
            var sendTimestamp = setter.ChildTriggerSetter.DateToTimestamp(now);
            var timeout = now - options.LostTriggerTimeout;

            emergencyTrigger.OffsetId = emergencyTrigger.OffsetId ?? await query.GetMinIdAsync(cancellationToken);
            if (emergencyTrigger.OffsetId is null)
            {
                // Нет ни одного подходящего.
                return false;
            }

            // Перебираем триггеры, выполняем проверки.
            await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
            {
                // I) Получаем информацию о триггере и процессе без блокировки
                // (взаимодействовать только через события).
                var triggers = await query.LoadAsync(
                    options.BatchSize,
                    emergencyTrigger.OffsetId!,
                    cancellationToken);

                if (!triggers.Any())
                {
                    // Все триггеры обработаны.
                    emergencyTrigger.OffsetId = default;
                    return false;
                }

                emergencyTrigger.OffsetId = triggers.Max(e => e.Value.triggerId);

                var notProcesseTriggers = triggers
                    .Select(e => e.Key)
                    .ToHashSet();

                {
                    var triggersEvents = new List<ITriggerEventRaiser<TId>.RaiseContainer>(triggers.Count / 2);
                    foreach (var elem in triggers.Values)
                    {
                        if (elem.ProcessDeleted || elem.ProcessStatus is ProcessStatusEnum.Complete)
                        {
                            // 1) Процесса нет - удаляем триггер.
                            // 2) Процесс завершен - удаляем триггер.
                            if (!elem.Trigger.IsCompleted)
                            {
                                // Тригер не завршен, блокировку не взяли - используем событие.
                                triggersEvents.Add(new ITriggerEventRaiser<TId>.RaiseContainer(
                                    options.TriggerEventQueue,
                                    elem.Trigger.ProcessId,
                                    new RemoveTriggerEvent(elem.Trigger.Key)
                                    )
                                    );
                            }
                            else 
                            {
                                // Триггер завершен,
                                // Такой триггер не реагирует на события,
                                // но считаем что конкуренции блокировки быть не должно.
                                setter.StandartSetter.ForRemove(elem.Trigger, value: true);
                            }
                            
                            notProcesseTriggers.Remove(elem.Trigger.Key);

                            // TODO: log warning;
                        }
                        else if (
                            setter.ChildTriggerSetter.IsChildTrigger(elem.Trigger, out var childTriggerState)
                            && childTriggerState.WaitDeliveryTimestamp.HasValue 
                            && now - setter.ChildTriggerSetter.TimestampToDate(childTriggerState.WaitDeliveryTimestamp.Value) > options.ChildTriggerDeliveryTimeout
                            )
                        {
                            // 3) Дочерний триггер не получил ответ от корневого триггера.
                            // TODO: range оптимизация.
                            var rootTriggerKey = await rootTriggerQuery.GetRootTriggerKeyAsync(elem.Trigger, cancellationToken);

                            if (!string.IsNullOrEmpty(rootTriggerKey))
                            {
                                // Повторно посылаем сигнал на корневой триггер, в ожидании что корневой триггер ответит.
                                triggersEvents.Add(new ITriggerEventRaiser<TId>.RaiseContainer(
                                    options.TriggerEventQueue,
                                    elem.Trigger.ProcessId,
                                    new SignalSimpleStreamTriggerEvent(
                                        triggerKey: rootTriggerKey,
                                        sendTriggerKey: elem.Trigger.Key,
                                        timeStamp: sendTimestamp,
                                        signals: elem.Trigger.SignalCode?.Bits
                                        )
                                    ));

                                setter.ChildTriggerSetter.RepeatSignalSended(elem.Trigger, childTriggerState, sendTimestamp);

                                // TODO: log warning. Повторная сигнал;
                            }
                            else
                            {
                                // Корневой триггер не найден, удаляем дочерний триггер.
                                // Тригер не завршен, блокировку не взяли - используем событие.
                                triggersEvents.Add(new ITriggerEventRaiser<TId>.RaiseContainer(
                                    options.TriggerEventQueue,
                                    elem.Trigger.ProcessId,
                                    new RemoveTriggerEvent(elem.Trigger.Key)
                                    )
                                    );

                                // TODO: log warning. Корневой триггер не найден.;
                            }

                            notProcesseTriggers.Remove(elem.Trigger.Key);

                            // TODO: log warning;
                        }
                        else if (
                            setter.StreamSetter.IsStreamTrigger(elem.Trigger)
                            && !elem.Trigger.IsActivated
                            && !elem.Trigger.IsCompleted)
                        {
                            // 4) Проверяем stream триггеры. Защита от потери события IProcessGoWaitStreamTriggerEvent.

                            // Триггер думает, что процесс активен, а процесс спит.
                            // Либл событе IProcessGoWaitStreamTriggerEvent еще обработалось, либо оно потерялось (смотрим на timeout).
                            var isWaitingMissmath =
                                !setter.StreamSetter.GetStreamsProcessIsWaiting(elem.Trigger)
                                && elem.ProcessStatus == ProcessStatusEnum.WaitEvent
                                && (now - elem.ReservationTimeout) > options.SteamGoWaitTimeout;

                            if (isWaitingMissmath)
                            {
                                // Публикуме событие, чтобы стрим перепроверил статус процесса.
                                // Не пытаемся делать это синхронно т.к. на триггер могут поступать сигналы событий.
                                triggersEvents.Add(new ITriggerEventRaiser<TId>.RaiseContainer(
                                    options.TriggerEventQueue,
                                    elem.Trigger.ProcessId,
                                    new RecheckProcessStatusStreamTriggerEvent(elem.Trigger.Key)
                                    )
                                    );

                                notProcesseTriggers.Remove(elem.Trigger.Key);

                                // TODO: log warning;

                            }
                        }
                        else if (
                            setter.StreamSetter.IsStreamTrigger(elem.Trigger) 
                            && elem.Trigger.Kind is ITriggerComponent.TriggerKind.SimpleStreamRoot
                            // TODO: && processRegistry.UseSignalCode(new ProcessTypeDto(elem.ProcessStatus)
                            )
                        {
                            // 5) Проверяем что был потерян IgnoreSignalCode.

                            var state = (ITriggerComponent.ISimpleStreamDto)elem.Trigger.State;

                            // Возможно событие об удаления кода из списка игнорирования было потеряно.
                            // (На триггере есть сигналы, но но триггер не активен, т.е. они не попадают в фильтр).
                            var needCheckProcess =
                                !elem.Trigger.SignalCode.Value.IsEmpty
                                // && elem.Trigger.FilterSignalCode.IsEmpty
                                && !elem.Trigger.IsActivated
                                && (now - elem.ReservationTimeout) > options.SteamGoWaitTimeout
                                && state.StreamsProcessIsWaiting;

                            var processsignalFilter = await query
                                .GetProcessSignalFilterAsync(elem.Trigger.ProcessId, cancellationToken);

                            if (processsignalFilter.Bits != elem.Trigger.SignalCodeFilter.Bits)
                            {
                                // Скорее всего событие было утеряно, посылаем триггеру событие, чтобы он перепроверил.
                                triggersEvents.Add(new ITriggerEventRaiser<TId>.RaiseContainer(
                                    options.TriggerEventQueue,
                                    elem.Trigger.ProcessId,
                                    new RecheckSignalFilterRootTriggerEvent(elem.Trigger.Key)
                                    ));

                                notProcesseTriggers.Remove(elem.Trigger.Key);

                                // TODO: log warning;
                            }
                        }
                    }

                    await triggerEventRaiser.RaiseAsync(triggersEvents, cancellationToken);
                }

                {
                    // II) Пробуем получить блокировку на триггер для вызова хендлера.
                    var forCheck = triggers.Values
                        .Where(e => notProcesseTriggers.Contains(e.Trigger.Key))
                        .Where(e => !options.IgnoreHandlers.Contains(e.Trigger.HandlerKey)) // Игнорируем хендлеры
                        .Where(e => 
                            !e.Trigger.IsCompleted
                            && !e.Trigger.IsActivated
                            && e.Trigger.Kind != ITriggerComponent.TriggerKind.SimpleStreamRoot // Он реагирует на сигналы дочерних.
                            && e.Trigger.SelectLockTimeout < timeout // Timeout резервирования превышает указанный (давно не брался в обработку)
                            )
                        .ToArray();

                    var lockedTriggerKeys = await query.LockSkipLockedAsync(
                        forCheck.Select(e => e.Trigger.Key).ToArray(),
                        cancellationToken);

                    var lockedTriggers = forCheck
                        .Select(e => e.Trigger)
                        .Where(e => lockedTriggerKeys.Contains(e.Key))
                        .ToArray();

                    var forHanler = lockedTriggers
                        .GroupBy(e => e.HandlerKey);

                    foreach (var elem in forHanler)
                    {
                        if (!triggerHandlerFactory.TryGetHandler(serviceProvider, elem.Key, out var handler))
                        {
                            // На текущей ноде не зарегистрирован хендлер (либо новая версия, либо не зарегистрирован).
                            continue;
                        }

                        var typedHandler = (ITriggerRangeHandler<TId>)handler;
                        var result = await typedHandler.CheckAsync(
                            elem,
                            isEmergencyTrigger: true,
                            cancellationToken);

                        var forExecute = new List<ITriggerComponent<TId>>(result.Count);
                        foreach (var elem2 in elem)
                        {
                            var elem2Result = result[elem2.Key];                            

                            if (elem2Result.NeedExecute)
                            {                                
                                forExecute.Add(elem2);

                                setter.OneOfTriggerSetter.OneOf(
                                    elem2,
                                    (1, 2),
                                    counterHandler: (state, _) =>
                                    {
                                        setter.CounterSetter.Activate(elem2, state);
                                    },
                                    timerHandler: (_) =>
                                    {
                                        setter.StandartSetter.SetActivated(elem2, value: true);
                                    },
                                    simpleStreamHandler: (state, _) =>
                                    {
                                        // Чтобы взвелся StreamsProcessIsWaiting.
                                        setter.SimpleStreamSetter.Activate(elem2, state);
                                    },
                                    offsetStreamHanler: (state, _) =>
                                    {
                                        // Чтобы взвелся StreamsProcessIsWaiting.
                                        setter.OffsetStreamSetter.Activate(elem2, state);
                                    }
                                    );
                            }

                            if (!setter.ChildTriggerSetter.IsChildTrigger(elem2, out var childTriggerState))
                            {
                                setter.StandartSetter.SetTriggerResult(
                                   elem2,
                                   elem2Result.Result);
                            }
                            else 
                            {
                                setter.ChildTriggerSetter.SetTriggerResult(
                                   elem2,
                                   childTriggerState,
                                   elem2Result.Result,
                                   sendTimestamp);
                            }                               
                        }

                        {
                            var awakened = await typedHandler.ExecuteAsync(forExecute, cancellationToken);
                            foreach (var elem3 in forExecute)
                            {
                                // Определяем, что процесс не был пробужден (По причине фильтрации сигналов).
                                if (!awakened.Contains(elem3.ProcessId))
                                {
                                    setter.OneOfTriggerSetter.OneOf(
                                        elem3,
                                        1,
                                        counterHandler: static (_, _) => { },
                                        timerHandler: static (_) => { },
                                        simpleStreamHandler: static (s, _) => s.StreamsProcessIsWaiting = true,
                                        offsetStreamHanler: static (s, _) => s.StreamsProcessIsWaiting = true
                                        );
                                }
                            }
                        }
                    }
                }

                await triggerRepository.SaveAsync(
                    triggers.Select(e => e.Value.Trigger).ToArray(), 
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);                
            }

            return true;
        }

        public interface IQueries
        {
            Task<TId?> GetMinIdAsync(CancellationToken cancellationToken);

            Task<IDictionary<string, StatusInfo>> LoadAsync(
                int batchSize,
                TId offsetId,
                CancellationToken cancellationToken);

            Task<HashSet<string>> LockSkipLockedAsync(
                ICollection<string> triggersKeys,
                CancellationToken cancellationToken
                );

            Task<BitFlagDto> GetProcessSignalFilterAsync(
                TId processId, 
                CancellationToken cancellationToken);

            public readonly record struct StatusInfo(
                TId triggerId,
                ITriggerComponent<TId> Trigger,
                bool ProcessDeleted,
                ProcessStatusEnum? ProcessStatus,

                DateTimeOffset? ReservationTimeout
                );
        }

        public class OptionsDto 
        {
            public string TriggerEventQueue { get; set; }

            public TimeSpan SoftTimeout { get; set; }
                = TimeSpan.FromMinutes(1);

            /// <summary>
            /// Допустимая задержку между тем, как процесс перешел в режим ожидания,
            /// а стрим триггер этого не увидел (не обработал событие IProcessGoWaitStreamTriggerEvent).
            /// </summary>
            public TimeSpan SteamGoWaitTimeout { get; set; }
                = TimeSpan.FromSeconds(30);

            /// <summary>
            /// Допустимая задержка, что был утерял сигнал.
            /// Триггер не активируется.
            /// </summary>
            public TimeSpan LostTriggerTimeout { get; set; }
                = TimeSpan.FromMinutes(5);

            public TimeSpan ChildTriggerDeliveryTimeout { get; set; }
                = TimeSpan.FromSeconds(10);
            

            public int BatchSize { get; set; }
                = 100;

            /// <summary>
            /// Для триггер обрабатывается собсветнным страхующим триггером или ему не нужен страхующий триггер.
            /// </summary>
            public HashSet<string> IgnoreHandlers { get; set; }
                = new HashSet<string>();

            public OptionsDto(string triggerEventQueue)
            {
                TriggerEventQueue = triggerEventQueue;
            }
        }
    }
}
