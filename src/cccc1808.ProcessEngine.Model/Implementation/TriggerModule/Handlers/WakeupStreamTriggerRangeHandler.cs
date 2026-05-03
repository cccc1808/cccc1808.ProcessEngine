using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers
{
    /// <summary>
    /// Хендлер пробуждения процессов-стримов.
    /// <see cref="ITriggerComponent{TId}.TriggerKind.SimpleStream"/> или <see cref="ITriggerComponent{TId}.TriggerKind.OffsetStream"/>.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class WakeupStreamTriggerRangeHandler<TId>
        : ITriggerRangeHandler<TId>
    {
        public const string Name = "WakeupStreamTriggerRangeHandler";

        private readonly IWakeupService<TId> _wakeUpService;

        public WakeupStreamTriggerRangeHandler(
            IWakeupService<TId> wakeUpService)
        {
            _wakeUpService = wakeUpService;
        }

        public virtual async ValueTask<IDictionary<string, ITriggerHandler.Result>> HandleAsync(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken)
        {
            await _wakeUpService.WakeupProcessHandlerAsync(
                triggers.Select(e => e.ProcessId).ToArray(),
                useShareLock: false,
                cancellationToken);

            return triggers.ToDictionary(
                e => e.Key, 
                e => new ITriggerHandler.Result(
                    NeedRepeat: true, 
                    IsActivated: false,
                    DateTimeOffset.MinValue));
        }
    }
}
