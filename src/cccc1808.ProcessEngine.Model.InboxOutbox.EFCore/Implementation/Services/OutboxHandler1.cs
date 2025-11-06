using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Batch;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.Dto.Componenets;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.Services
{
    public class OutboxHandler1<TId>
        : BaseEFChangeTrackerIJobHandler1<TId>
    {
        private readonly IQueueProviderFactory _queueProviderFactory;
        

        public OutboxHandler1(
            IProcessRepository<TId> repository,
            IProcessSetter setter,
            IQueueProviderFactory queueProviderFactory)
            : base(
                  repository,
                  setter)
        {
            _queueProviderFactory = queueProviderFactory;
        }

        public override async ValueTask HandleRangeAsync(
            IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes, 
            CancellationToken cancellationToken)
        {
            var context = processes.ToDictionary(
                e => e.Key.Id,
                e => (Process: 
                    e.Value,
                    outbox: e.Value.GetComponent<OutboxProcessComponent<TId>>(), 
                    softTimeout: e.Value.GetComponent<ISoftTimeoutComponent>()));

            var groupByQueue = context.Values
                .Where(e => e.outbox.Messages.Any())
                .GroupBy(e => e.outbox.Data.Queue)
                .ToArray();

            // TODO: обработка ошибок
            foreach (var elem in groupByQueue)
            {
                var queueBatch = elem
                    .SelectMany(e1 => e1.outbox.Messages.Select(e2 => (Data: e1, Message: e2))
                    .OrderByDescending(e => e.Message.Priority)
                    .ThenBy(e => e.Message.OrderId)
                    .Select(e => (
                        Message: e,
                        producerMessage: new MessageDto(
                            e.Message.Key,
                            elem.Key.Name,
                            e.Message.Headers.Deserialize<HeaderDto[]>() ?? Array.Empty<HeaderDto>(),
                            e.Message.Body,
                            e.Message.Partition
                            ))))
                    .ToArray();

                var producer = await _queueProviderFactory.GetProducerAsync(elem.Key.Name, cancellationToken);
                try
                {
                    await producer.ProduceBatchAsync(
                        queueBatch.Select(e => e.producerMessage).ToArray(), 
                        cancellationToken);

                    foreach (var elem2 in queueBatch)
                    {
                        elem2.Message.Message.IsActive = false;
                        elem2.Message.Message.Status = OutboxMessageDbEntity<TId>.StatusEnum.Complete;
                        elem2.Message.Message.SendDate = DateTimeOffset.UtcNow;
                        elem2.Message.Data.outbox.ProcessCount++;
                    }
                }
                catch (Exception ex)
                {
                    foreach (var elem2 in elem) 
                    {
                        _setter.SetError(elem2.Process, ex, allowRetry: true);
                    }
                }
            }

            // Стримы у которых все сообщения обработаны - засыпают.
            foreach (var elem in context.Values)
            {
                if (elem.outbox.UnreadCount == elem.outbox.ProcessCount)
                {
                    _setter.SetStatus(elem.Process, ProcessStatusEnum.WaitEvent);
                }
            }
        }
    }
}
