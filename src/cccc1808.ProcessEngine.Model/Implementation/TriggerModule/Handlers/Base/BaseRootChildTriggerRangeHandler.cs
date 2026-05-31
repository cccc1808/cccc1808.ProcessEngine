using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
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
        private readonly ITriggerHandlerFacade<TId> _handlerFacade;

        public BaseRootChildTriggerRangeHandler(
            ITriggerHandlerFacade<TId> handlerFacade)
        {
            _handlerFacade = handlerFacade;
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
            await _handlerFacade.RaiseSignalToRootTriggerAsync(triggers, info, cancellationToken);            
        }

        protected abstract Task<IDictionary<string, ITriggerHandlerFacade<TId>.RootEventInfoDto>> GetEventInfoAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken);
    }
}
