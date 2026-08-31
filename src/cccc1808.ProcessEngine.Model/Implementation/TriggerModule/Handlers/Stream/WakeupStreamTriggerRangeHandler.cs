using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Stream
{
    /// <summary>
    /// Хендлер пробуждения процессов-стримов.
    /// <see cref="ITriggerComponent.TriggerKind.SimpleStream"/> или <see cref="ITriggerComponent{TId}.TriggerKind.OffsetStream"/>.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class WakeupStreamTriggerRangeHandler<TId>
        : ITriggerRangeHandler<TId>
    {
        public const string Name = "WakeupStreamTriggerRangeHandler";

        private readonly ITriggerHandlerFacade<TId> _triggerHandlerFacade;


        public WakeupStreamTriggerRangeHandler(
            ITriggerHandlerFacade<TId> triggerHandlerFacade)
        {
            _triggerHandlerFacade = triggerHandlerFacade;
        }

        public async ValueTask<IDictionary<string, ITriggerRangeHandler<TId>.ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            bool isEmergencyTrigger, 
            CancellationToken cancellationToken)
        {
            if (isEmergencyTrigger)
            {
                // Если мы попали в Emengency триггер, то делаем проверку состояния процесса.

                var check = await _triggerHandlerFacade.CheckCompleteOrNotFound(
                    triggers,
                    cancellationToken);

                var result = new Dictionary<string, ITriggerRangeHandler<TId>.ResultDto>();
                foreach (var elem in check.InComplete)
                {
                    result.Add(
                        elem.Key,
                        new ITriggerRangeHandler<TId>.ResultDto(
                            ITriggerHandler.ResultDto.RemoveResult(),
                            NeedExecute: false)
                        );
                }

                foreach (var elem in check.NotFound)
                {
                    result.Add(
                        elem.Key,
                        new ITriggerRangeHandler<TId>.ResultDto(
                            ITriggerHandler.ResultDto.RemoveResult(),
                            NeedExecute: false)
                        );
                }
            }

            // Брать блокировку процесса тут не пытается т.к. это wakeup (конкурентный).

            return triggers.ToDictionary(
                e => e.Key,
                e => new ITriggerRangeHandler<TId>.ResultDto(
                    ITriggerHandler.ResultDto.ActivateResult(),
                    NeedExecute: true
                    ));
        }

        public async ValueTask<ISet<TId>> ExecuteAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken)
        {
            var processIds = triggers.Select(e => e.ProcessId).ToHashSet();

            await _triggerHandlerFacade.ToAsyncExecutingWakeupAsync(
                processIds,
                cancellationToken);

            return processIds;
        }
    }
}
