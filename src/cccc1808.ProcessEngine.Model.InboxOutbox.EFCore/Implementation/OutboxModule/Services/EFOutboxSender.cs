using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Services
{
    public class EFOutboxSender<TId> : IOutboxSender<TId>
    {
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IEFDbContext _dbContext;
        private readonly ITriggerEventRaiser _triggerEventRaiser;
        private readonly IHeaderJsonSerializer _headerJsonSerializer;
        private readonly IClassifierRepository<TId> _classifierRepository;
        private readonly OutboxRegistryDto _outboxRegistry;

        public EFOutboxSender(
            IIdGenerator<TId> idGenerator,
            IEFDbContext dbContext,
            ITriggerEventRaiser triggerEventRaiser,
            IHeaderJsonSerializer headerJsonSerializer,
            IClassifierRepository<TId> classifierRepository,
            OutboxRegistryDto outboxRegistry)
        {
            _idGenerator = idGenerator;
            _dbContext = dbContext;
            _triggerEventRaiser = triggerEventRaiser;
            _headerJsonSerializer = headerJsonSerializer;
            _classifierRepository = classifierRepository;
            _outboxRegistry = outboxRegistry;
        }

        public async ValueTask SendAsync(
            ICollection<(AggregateDto aggregate, MessageDto message)> messages,
            CancellationToken cancellationToken)
        {
            var groups = messages
                .GroupBy(e => (e.aggregate, e.message.Queue))
                .ToArray();

            var outboxes = await _classifierRepository.GetOutboxInfoAsync(
                groups.Select(e => e.Key).ToArray(),
                cancellationToken);

            var messageSet = _dbContext.Set<OutboxMessageDbEntity<TId>>();
            var timestamps = new Dictionary<(AggregateDto Aggreagate, string Queue), long>(groups.Length);
            foreach (var elem in messages)
            {
                var key = (elem.aggregate, elem.message.Queue);

                var entity = new OutboxMessageDbEntity<TId>(
                    id: await _idGenerator.NextAsync(cancellationToken),
                    partition: 0,
                    priority: 1,
                    orderId: await _classifierRepository.GetOutboxOrderIdAsync(key, cancellationToken),
                    processId: outboxes[key].ProcessId,
                    isActive: true,
                    key: elem.message.Key,
                    idemporencyId: Guid.NewGuid().ToString(),
                    body: elem.message.Body,
                    headers: _headerJsonSerializer.Serialize(elem.message.Headers),
                    sendDate: null
                    );
                messageSet.Add(entity);

                timestamps[key] = entity.OrderId;
            }

            // Для пробуждения outbox процесса
            await _triggerEventRaiser.RaiseAsync(
                groups
                    .Select(e => new SignalStreamTriggerEvent(
                        outboxes[e.Key].TriggerKey,
                        channelName: _outboxRegistry.TriggerChannelName,
                        channelTimestamp: timestamps[e.Key]))
                    .ToArray(),
                cancellationToken);
        }
    }
}
