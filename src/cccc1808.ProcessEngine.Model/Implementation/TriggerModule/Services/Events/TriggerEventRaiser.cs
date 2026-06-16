using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events
{
    public class TriggerEventRaiser<TId> 
        : ITriggerEventRaiser<TId>
    {
        private readonly IQueueProviderFactory _queueProviderFactory;
        private readonly TriggerOptions<TId> _triggerOptions;
        private readonly IEventJsonSerializer _eventJsonSerializer;

        public TriggerEventRaiser(
            IQueueProviderFactory queueProviderFactory, 
            TriggerOptions<TId> triggerOptions, 
            IEventJsonSerializer eventJsonSerializer)
        {
            _queueProviderFactory = queueProviderFactory;
            _triggerOptions = triggerOptions;
            _eventJsonSerializer = eventJsonSerializer;
        }
        
        public async ValueTask RaiseAsync(
            ICollection<ITriggerEventRaiser<TId>.RaiseContainer> events, 
            CancellationToken cancellationToken)
        {
            if(!events.Any())
            {
                return;
            }

            foreach (var elem in events.GroupBy(e => e.EventQueue))
            {
                var producer = await _queueProviderFactory.GetProducerAsync(elem.Key, cancellationToken);

                await producer.ProduceBatchAsync(
                    elem
                        .Select(e => new MessageDto(
                            Key: Guid.NewGuid().ToString(),
                            Queue: e.EventQueue,
                            Headers: [],
                            Body: _eventJsonSerializer.Serialize(e.Event),
                            Partition: _triggerOptions.PartitionSelector(e) ?? -1
                            ))
                        .ToArray(),
                    cancellationToken);
            }
        }

        public void ClearBuffer()
        {
            // Тут нет буфера.
        }
    }
}
