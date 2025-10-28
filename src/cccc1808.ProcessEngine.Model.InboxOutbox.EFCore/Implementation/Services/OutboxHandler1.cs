using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Common.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider;
using cccc1808.ProcessEngine.Model.MessageStream.EFCore.Implementation.Entities.Conditions;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Abstract;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.Services
{
    public class OutboxHandler1<TId, TDbContext>
        : BaseEFChangeTrackerIJobHandler1<TId, TDbContext>
        where TDbContext : DbContext
    {
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IQueueProviderFactory _queueProviderFactory;
        private readonly IMessageStreamTechService<TId> _messageStreamTechService;
        private readonly MessageDbEntity_ForProcessgByStream1_RangeCondition<TId> messageDbEntity_ForProcessgByStream1_RangeCondition;

        public OutboxHandler1(
            TDbContext dbContext,
            IProcessRepository<TId> repository,
            IProcessSetter setter,
            ILockQueryHintStore lockQueryHintStore,
            IQueueProviderFactory queueProviderFactory,
            IMessageStreamTechService<TId> messageStreamTechService)
            : base(
                  dbContext,
                  repository,
                  setter)
        {
            _lockQueryHintStore = lockQueryHintStore;
            _queueProviderFactory = queueProviderFactory;
            _messageStreamTechService = messageStreamTechService;
            messageDbEntity_ForProcessgByStream1_RangeCondition = new MessageDbEntity_ForProcessgByStream1_RangeCondition<TId>();      
        }

        public override async ValueTask HandleRangeAsync(
            IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes, 
            CancellationToken cancellationToken)
        {
            await _messageStreamTechService.BeforeStreamExecuteAsync(
                processes.Values.ToArray(),
                cancellationToken);

            var outboxComponenets = processes.Values
                .Select(e => e.GetComponent<OutboxStreamDataDbEntity<TId>>())
                .ToDictionary(e => e.Id, e => e);

            (MessageDbEntity<TId> StreamMessage, OutboxMessageDataDbEntity<TId> OutboxMessage)[] messages;
            using (var hintScope = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var data = await _dbContext.Set<MessageDbEntity<TId>>()
                    .ApplayFilterCondition(
                        messageDbEntity_ForProcessgByStream1_RangeCondition,
                        processes.Values.Select(e => e.Id).ToArray()
                        )
                    .Join(
                        _dbContext.Set<OutboxMessageDataDbEntity<TId>>(), 
                        e => e.Id, 
                        e => e.Id, 
                        (e1, e2) => new { e1, e2 })
                    .Take(250)
                    .ToArrayAsync(cancellationToken);

                messages = data
                    .Select(e => (e.e1, e.e2))
                    .ToArray();
            }

            var groupByStream = messages
                .GroupBy(e => e.StreamMessage.StreamId)
                .ToDictionary(e => e.Key, e => e);

            var groupByQueue = outboxComponenets.Values
                .Where(e => groupByStream[e.Id].Any())
                .GroupBy(e => e.Queue)
                .ToArray();

            // TODO: обработка ошибок
            foreach (var elem in groupByQueue)
            {
                var queueBatch = groupByQueue
                    .SelectMany(e => e.SelectMany(e2 => groupByStream[e2.Id]))
                    .OrderByDescending(e => e.StreamMessage.Priority)
                    .ThenBy(e => e.StreamMessage.OrderId)
                    .Select(e => (
                        e.StreamMessage,
                        e.OutboxMessage, 
                        producerMessage: new MessageDto(
                            e.OutboxMessage.Key,
                            outboxComponenets[e.StreamMessage.Id].Queue.Name,
                            e.OutboxMessage.Headers.Deserialize<HeaderDto[]>() ?? Array.Empty<HeaderDto>(),
                            e.OutboxMessage.Body,
                            e.OutboxMessage.Partition
                            )))
                    .ToArray();

                var producer = await _queueProviderFactory.GetProducerAsync(elem.Key.Name, cancellationToken);
                try
                {
                    await producer.ProduceBatchAsync(
                        queueBatch.Select(e => e.producerMessage).ToArray(), 
                        cancellationToken);

                    foreach (var elem2 in queueBatch)
                    {
                        elem2.StreamMessage.IsActive = false;
                        elem2.OutboxMessage.Status = OutboxMessageDataDbEntity<TId>.StatusEnum.Complete;
                        elem2.OutboxMessage.SendDate = DateTimeOffset.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    var streamIds = groupByQueue
                        .SelectMany(e => e.Select(e2 => groupByStream[e2.Id].Key))
                        .ToArray();

                    foreach (var elem2 in streamIds) 
                    {
                        _setter.SetError(processes[new ProcessIdDto<TId>(elem2)], ex);
                    }
                }
            }

            // Сбрасываем selectDate т.к. мы могли обработать не все сообщения во всех стрим, а блокировку на них сейчас держим.
            // Можно сделать более хитрую политику.
            foreach (var elem in processes.Values)
            {
                if (elem.CurrentSession.HaveError)
                {
                    continue;
                }

                _setter.SetTimer(elem, DateTimeOffset.MinValue.UtcDateTime);
            }

            await _messageStreamTechService.AfterStreamExecuteAsync(
                    processes.Values.ToArray(),
                    cancellationToken);
        }
    }
}
