using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class RootTriggerService<TId> : IRootTriggerService<TId>
    {
        private readonly ITriggerEventRaiser<TId> _eventRaiser;
        private readonly IRootTriggerService<TId>.IQueries _queries;

        private readonly OptionsDto _options;

        public RootTriggerService(
            ITriggerEventRaiser<TId> eventRaiser,
            IRootTriggerService<TId>.IQueries queries,

            OptionsDto options)
        {
            _eventRaiser = eventRaiser;
            _queries = queries;

            _options = options;
        }

        public async Task SignalToRootTriggerAsync(
            ICollection<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken)
        {
            var toRootTriggerEvents = triggers
                .Select(               
                    e => new ITriggerEventRaiser<TId>.RaiseContainer(
                        _options.RootSignalQueue, 
                        e.ProcessId, 
                        new SignalSimpleStreamTriggerEvent(e.RootTriggerKey)                    
                        )
                )
                .ToArray();

            await _eventRaiser.RaiseAsync(
                toRootTriggerEvents, 
                cancellationToken);
        }

        public async Task RootTriggerProcessGoSleepAsync(
            ICollection<ITriggerComponent<TId>> rootTriggers,
            CancellationToken cancellationToken)
        {
            if (!rootTriggers.Any())
            {
                return;
            }

            var childs = await _queries.GetChildTriggersForRootTriggerProcessGoSleepAsyncAsync(rootTriggers, cancellationToken);

            var toChildTriggersEvents = childs
                .Select(
                    e => new ITriggerEventRaiser<TId>.RaiseContainer(
                        _options.GoSleepSignalQueue,
                        e.ProcessId,
                        new ProcessGoWaitStreamTriggerEvent(e.Key)
                        )
                )
                .ToArray();

            await _eventRaiser.RaiseAsync(
                toChildTriggersEvents,
                cancellationToken);
        }


        public class OptionsDto 
        {
            /// <summary>
            /// TODO: Возможно оформить по другому
            /// </summary>
            public string RootSignalQueue { get; set; }

            /// <summary>
            /// TODO: Возможно оформить по другому
            /// </summary>
            public string GoSleepSignalQueue { get; set; }

            public OptionsDto(
                string rootSignalQueue, 
                string goSleepSignalQueue)
            {
                RootSignalQueue = rootSignalQueue;
                GoSleepSignalQueue = goSleepSignalQueue;
            }
        }
    }
}
