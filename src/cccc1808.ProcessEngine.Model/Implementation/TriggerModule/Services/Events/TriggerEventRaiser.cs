using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events
{
    public class TriggerEventRaiser<TId> 
        : ITriggerEventRaiser<TId>
    {
        private readonly IQueueProviderFactory _queueProviderFactory;
        private readonly TriggerOptions<TId> _triggerOptions;
        private readonly IEventJsonSerializer _eventJsonSerializer;
        private readonly OptionsDto _options;

        public TriggerEventRaiser(
            IQueueProviderFactory queueProviderFactory,
            TriggerOptions<TId> triggerOptions,
            IEventJsonSerializer eventJsonSerializer,
            OptionsDto options)
        {
            _queueProviderFactory = queueProviderFactory;
            _triggerOptions = triggerOptions;
            _eventJsonSerializer = eventJsonSerializer;
            _options = options;
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

        public async ValueTask RaiseProcessAsyncExecuting(
            ICollection<ProcessAsyncExecuteMessageDto<TId>> messages, 
            CancellationToken cancellationToken)
        {
            if (!messages.Any())
            {
                return;
            }

            var m = messages
                .Select(e => new MessageDto(
                    Key: Guid.NewGuid().ToString(),
                    Queue: _options.RunnerQueue!,
                    Headers: [], 
                    Body: JsonHelper.ToJsonElement(e),
                    Partition: -1))
                .ToArray();

            var producer = await _queueProviderFactory.GetProducerAsync(_options.RunnerQueue!, cancellationToken);
            await producer.ProduceBatchAsync(m, cancellationToken);
        }

        public void ClearBuffer()
        {
            // Тут нет буфера.
        }

        public class OptionsDto 
        {
            public string? RunnerQueue { get; set; }
        }
    }
}
