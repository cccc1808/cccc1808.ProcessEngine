using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services
{
    public class TriggerStateService<TId> 
        : ITriggerStateService<TId>
    {
        private readonly ITriggerEventRaiser<TId> _eventRaiser;

        public TriggerStateService(ITriggerEventRaiser<TId> eventRaiser)
        {
            _eventRaiser = eventRaiser;
        }

        public async ValueTask RemoveTriggerActionCompleteAsync(
            IProcessContainer<TId> process, 
            string actionId, 
            CancellationToken cancellationToken)
        {
            if (!process.TryGetComponent<IProcessStateWithTriggers>(out var triggerState))
            {
                return;
            }

            var forRemove = triggerState.TriggerState.Triggers.Values
                .Where(e => e.RemoveIfActionComplete && e.RemoveActionIds.Contains(actionId))
                .ToArray();

            if (!forRemove.Any())
            {
                return;
            }

            await ProcessAsync(process.Id, triggerState, forRemove, cancellationToken);
        }

        public async ValueTask RemoveTriggersMoveToken(
            IProcessContainer<TId> process,
            string tokenId,
            CancellationToken cancellationToken)
        {
            if (!process.TryGetComponent<IProcessStateWithTriggers>(out var triggerState))
            {
                return;
            }

            var forRemove = triggerState.TriggerState.Triggers.Values
                .Where(e =>
                    e.RemoveIfTokenMove
                    && (e.RemoveTokenId is null || e.RemoveTokenId == tokenId))
                .ToArray();

            if (!forRemove.Any())
            {
                return;
            }

            await ProcessAsync(process.Id, triggerState, forRemove, cancellationToken);
        }

        public async ValueTask RemoveTriggersProcessCompleteAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            if (!process.TryGetComponent<IProcessStateWithTriggers>(out var triggerState))
            {
                return;
            }

            var forRemove = triggerState.TriggerState.Triggers.Values
                .Where(e => e.RemoveIfProcessComplete)
                .ToArray();

            await ProcessAsync(process.Id, triggerState, forRemove, cancellationToken);
        }

        private async ValueTask ProcessAsync(
            TId processId,
            IProcessStateWithTriggers component,
            TriggerStateContainer.TriggerInfo[] triggers,
            CancellationToken cancellationToken)
        {
            var events = new List<ITriggerEventRaiser<TId>.RaiseContainer>(triggers.Length);

            foreach (var elem in triggers)
            {
                events.Add(
                    new ITriggerEventRaiser<TId>.RaiseContainer(
                        elem.RemoveTriggerQueueName,
                        processId,
                        new RemoveTriggerEvent(elem.Key)
                        )
                    );

                component.TriggerState.Triggers.Remove(elem.Key);
            }

            await _eventRaiser.RaiseAsync(
                events,
                cancellationToken);
        }
    }
}
