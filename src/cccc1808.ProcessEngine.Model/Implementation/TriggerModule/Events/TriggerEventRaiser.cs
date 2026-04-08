using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class TriggerEventRaiser : ITriggerEventRaiser
    {
        private readonly IQueueProviderFactory _queueProviderFactory;
        private readonly TriggerOptions _triggerOptions;
        private readonly IEventJsonSerializer _eventJsonSerializer;

        public TriggerEventRaiser(
            IQueueProviderFactory queueProviderFactory, 
            TriggerOptions triggerOptions, 
            IEventJsonSerializer eventJsonSerializer)
        {
            _queueProviderFactory = queueProviderFactory;
            _triggerOptions = triggerOptions;
            _eventJsonSerializer = eventJsonSerializer;
        }

        public async ValueTask RaiseAsync(
            ICollection<ITriggerEvent> events, 
            CancellationToken cancellationToken)
        {
            var producer = await _queueProviderFactory.GetProducerAsync(_triggerOptions.TriggerEventQueueName, cancellationToken);

            await producer.ProduceBatchAsync(
                events
                    .Select(e => new MessageDto(
                        Key: Guid.NewGuid().ToString(),
                        Queue: _triggerOptions.TriggerEventQueueName,
                        Headers: [],
                        Body: _eventJsonSerializer.Serialize(e),
                        Partition: -1
                        ))
                    .ToArray(),
                cancellationToken);
        }
    }
}
