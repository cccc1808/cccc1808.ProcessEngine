using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Base
{
    /// <summary>
    /// Хендлер для дочерних триггеров при использовании <see cref="ITriggerComponent.TriggerKind.SimpleStreamRoot"/>.
    /// Передает сигнал не на процесс, а на root триггер.
    /// 
    /// https://wiki.denhome.ru/bin/view/Проекты%20и%20репозитории/Библиотеки/Движок%20cccc1808.%20ProcessEngine/Про%20передачу%20сигнала%20на%20процесс/
    /// Типы передачи сигналов 2.2.1
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public abstract class BaseRootChildTriggerRangeHandler<TId>
        : ITriggerRangeHandler<TId>
    {
        private readonly ITriggerSetter<TId> _triggerSetter;
        private readonly ITriggerEventRaiser<TId> _triggerEventRaiser;

        public BaseRootChildTriggerRangeHandler(
            ITriggerSetter<TId> triggerSetter,
            ITriggerEventRaiser<TId> triggerEventRaiser)
        {
            _triggerSetter = triggerSetter;
            _triggerEventRaiser = triggerEventRaiser;
        }

        public abstract ValueTask<IDictionary<string, ITriggerRangeHandler<TId>.ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            bool isEmergencyTrigger,
            CancellationToken cancellationToken);

        public async ValueTask ExecuteAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken)
        {
            var info = await GetEventInfoAsync(triggers, cancellationToken);

            var toRootTriggerEvents = triggers
                 .Select(
                    e => new ITriggerEventRaiser<TId>.RaiseContainer(
                        info[e.Key].Queue,
                        e.ProcessId,
                        new SignalSimpleStreamTriggerEvent(
                            info[e.Key].RootTriggerKey,
                            sendTriggerKey: e.Key,
                            timeStamp: _triggerSetter.ChildTriggerSetter.IsChildTrigger(e, out var childState) 
                                ? childState.WaitDeliveryTimestamp ?? throw new Exception(
                                    $"[Bug] Ожидается запоненое значение {nameof(ITriggerComponent.IChildTriggerDto.WaitDeliveryTimestamp)}"
                                    )
                                : throw new Exception("[Bug] Ожидается дочерний триггер.")
                            )
                        )
                    )
                .ToArray();

            await _triggerEventRaiser.RaiseAsync(
                toRootTriggerEvents,
                cancellationToken);           
        }

        protected abstract Task<IDictionary<string, ITriggerHandlerFacade<TId>.RootEventInfoDto>> GetEventInfoAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken);
    }
}
