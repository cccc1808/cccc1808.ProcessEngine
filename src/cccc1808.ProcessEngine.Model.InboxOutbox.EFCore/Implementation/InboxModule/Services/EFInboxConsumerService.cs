using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Services
{
    public class EFInboxConsumerService<TId>
        : IInboxConsumerService
    {
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IEFDbContext _dbContext;
        private readonly ITriggerEventRaiser<TId> _triggerRaiser;
        private readonly IClassifierRepository<TId> _classifierRepository;
        private readonly IHeaderJsonSerializer _headerJsonSerializer;
        private readonly InboxRegistryDto _inboxRegistry;
        private readonly Options _options;

        public EFInboxConsumerService(
            IIdGenerator<TId> idGenerator,
            IEFDbContext dbContext,
            ITriggerEventRaiser<TId> triggerRaiser,
            IClassifierRepository<TId> classifierRepository,
            IHeaderJsonSerializer headerJsonSerializer,
            InboxRegistryDto inboxRegistry,
            Options options)
        {
            _idGenerator = idGenerator;
            _dbContext = dbContext;
            _triggerRaiser = triggerRaiser;
            _classifierRepository = classifierRepository;
            _headerJsonSerializer = headerJsonSerializer;
            _inboxRegistry = inboxRegistry;
            _options = options;
        }

        public async ValueTask ProcessBatchAsync(
            ICollection<MessageDto> batch,
            CancellationToken cancellationToken)
        {
            // 1) 
            // TODO: уникальность сообщений.
            var aggregateIdMapping = batch.ToDictionary(e => e.Key, e => _options.AggregateIdFactory(e));

            var inboxData = await _classifierRepository.GetInboxInfoAsync(
                batch.Select(e => (aggregateIdMapping[e.Key], e.Queue)).Distinct().ToArray(),
                cancellationToken
                );
            
            // 2) Записываем сообщения и запускаем стрим.
            {
                var forInsert = new List<InboxMessageDbEntity<TId>>(batch.Count);
                foreach (var elem in batch)
                {
                    var aggregate = aggregateIdMapping[elem.Key];
                    var inbox = inboxData[(aggregate, elem.Queue)];

                    forInsert.Add(
                        new InboxMessageDbEntity<TId>(
                            id: await _idGenerator.NextAsync(cancellationToken),
                            priority: 0,
                            orderId: await _classifierRepository.GetInboxOrderIdAsync((aggregate, elem.Queue), cancellationToken),
                            processId: inbox.ProcessId,
                            isActive: true,
                            key: elem.Key,
                            partition: elem.Partition,
                            idemporencyId: _options.IdempotencyIdFactory(elem),
                            body: elem.Body,
                            headers: _headerJsonSerializer.Serialize(elem.Headers))
                        );
                }

                // Обработка повторяющися сообщений(IdemporencyId)
                var result = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                    .UpsertRange(forInsert)
                    .On(e => new { e.ProcessId, e.IdempotencyId })
                    .NoUpdate()
                    .RunAndReturnAsync(cancellationToken);

                if (result.Any())
                {
                    var processData = inboxData
                        .ToDictionary(e => e.Value.ProcessId, e => e.Value);

                    // Передаем сигнал о поступлении новых сообщений на триггер.
                    var triggerEvents = result
                        .Select(e => e.ProcessId)
                        .Distinct()
                        .Select(e => processData[e])
                        .Select(e => new ITriggerEventRaiser<TId>.RaiseContainer(
                            _inboxRegistry.TriggerEventQueue,
                            e.ProcessId,
                            new SignalOffsetTriggerEvent(
                                e.TriggerKey,
                                updateOffset: result.Max(e => e.OrderId)
                                )
                            )
                        )
                        .ToArray();
                    
                    await _triggerRaiser.RaiseAsync(
                        triggerEvents,
                        cancellationToken);
                }                
            }            
        }

        public class Options 
        {
            public Func<MessageDto, AggregateDto> AggregateIdFactory { get; set; } = null!;
            public Func<MessageDto, string> IdempotencyIdFactory { get; set; } = null!;
        }
    }
}
