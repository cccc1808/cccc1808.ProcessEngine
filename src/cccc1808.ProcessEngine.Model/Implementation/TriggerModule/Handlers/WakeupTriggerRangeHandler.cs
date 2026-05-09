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
    /// Хендлер пробуждения процессов.
    /// Используется для реализации:
    /// * Retry
    /// * Таймеров
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class WakeupTriggerRangeHandler<TId>
        : ITriggerRangeHandler<TId>
    {
        public const string Name = "WakeupTriggerRangeHandler";

        private readonly IWakeupService<TId> _wakeUpService;

        public WakeupTriggerRangeHandler(
            IWakeupService<TId> wakeUpService)
        {
            _wakeUpService = wakeUpService;
        }

        public async ValueTask<IDictionary<string, ITriggerHandler.Result>> HandleAsync(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken)
        {
            await _wakeUpService.WakeupProcessHandlerAsync(
                triggers.Select(e => e.ProcessId).ToArray(),
                useShareLock: true,
                cancellationToken);

            return triggers.ToDictionary(
                e => e.Key, 
                e => new ITriggerHandler.Result(
                    NeedRepeat: false, 
                    IsActivated: false,
                    DateTimeOffset.MinValue));
        }
    }
}
