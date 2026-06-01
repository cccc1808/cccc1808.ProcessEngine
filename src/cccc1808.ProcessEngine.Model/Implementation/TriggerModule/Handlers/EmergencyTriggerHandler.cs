using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
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
            var triggerHandlerFactory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
            var triggerRepository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
            var triggerEventRaiser = serviceProvider.GetRequiredService<ITriggerEventRaiser<TId>>();
            var setter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();

            var now = dateTimeProvider.UtcNow;
            var timeout = dateTimeProvider.UtcNow - options.LostTriggerTimeout;

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
                        var isStreamTrigger = elem.Trigger.Kind
                            is ITriggerComponent.TriggerKind.SimpleStream
                            or ITriggerComponent.TriggerKind.OffsetStream
                            or ITriggerComponent.TriggerKind.SimpleStreamRoot;

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
                            isStreamTrigger
                            && !elem.Trigger.IsActivated
                            && !elem.Trigger.IsCompleted)
                        {
                            // 3) Проверяем stream триггеры. Защита от потери события IProcessGoWaitStreamTriggerEvent.

                            setter.OneOfSetter.OneOfTrigger(
                                elem.Trigger,
                                (1, 2),
                                counterHandler: (_, _) => { },
                                timerHandler: (_) => { },
                                simpleStreamHandler: (state, p) =>
                                {
                                    // Триггер думает, что процесс активен, а процесс спит.
                                    // Либо событе IProcessGoWaitStreamTriggerEvent еще обработалось, либо оно потерялось (смотрим на timeout).
                                    var isWaitingMissmath =
                                        !state.StreamsProcessIsWaiting
                                        && elem.ProcessStatus == ProcessStatusEnum.WaitEvent
                                        && (now - elem.SelectLockTimeout) > options.SteamGoWaitTimeout;

                                    if (isWaitingMissmath)
                                    {
                                        triggersEvents.Add(new ITriggerEventRaiser<TId>.RaiseContainer(
                                            options.TriggerEventQueue,
                                            elem.Trigger.ProcessId,
                                            new RecheckProcessStatusStreamTriggerEvent(elem.Trigger.Key)
                                            )
                                            );

                                        // TODO: log warning;
                                    }
                                },
                                offsetStreamHanler: (state, p) =>
                                {
                                    // Триггер думает, что процесс активен, а процесс спит.
                                    // Либо событе IProcessGoWaitStreamTriggerEvent еще обработалось, либо оно потерялось (смотрим на timeout).
                                    var isWaitingMissmath =
                                        !state.StreamsProcessIsWaiting
                                        && elem.ProcessStatus == ProcessStatusEnum.WaitEvent
                                        && (now - elem.SelectLockTimeout) > options.SteamGoWaitTimeout;

                                    if (isWaitingMissmath)
                                    {
                                        triggersEvents.Add(new ITriggerEventRaiser<TId>.RaiseContainer(
                                             options.TriggerEventQueue,
                                             elem.Trigger.ProcessId,
                                             new RecheckProcessStatusStreamTriggerEvent(elem.Trigger.Key)
                                             )
                                             );

                                        // TODO: log warning;
                                    }
                                }
                                );

                            notProcesseTriggers.Remove(elem.Trigger.Key);
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
                            setter.StandartSetter.SetTriggerResult(
                                elem2,
                                elem2Result.Result);

                            if (elem2Result.NeedExecute)
                            {
                                forExecute.Add(elem2);
                            }
                        }
                        await typedHandler.ExecuteAsync(forExecute, cancellationToken);
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

            public readonly record struct StatusInfo(
                TId triggerId,
                ITriggerComponent<TId> Trigger,
                bool ProcessDeleted,
                ProcessStatusEnum? ProcessStatus,
                DateTimeOffset? SelectLockTimeout
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
