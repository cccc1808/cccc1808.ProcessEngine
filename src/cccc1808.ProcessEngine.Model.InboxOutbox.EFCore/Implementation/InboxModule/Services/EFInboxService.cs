using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Conditions;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.CommonModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Services
{
    public class EFInboxService<TId>
        : DbContext
    {
        private readonly IEFDbContext _dbContext;
        private readonly InboxRegistryDto _inboxRegistryDto;
        private readonly IAggregateClassifierDbEntityCondition<TId> _aggregateClassifierDbEntityCondition;
        private readonly ITriggerEventRaiser _triggerRaiser;

        private readonly Func<string, TId> _idFactory;
        private readonly Func<MessageDto, AggregateDto> _aggregateIdFactory;
        private readonly Func<MessageDto, string> _idempotencyIdFactory;

        public EFInboxService(
            IEFDbContext dbContext,
            InboxRegistryDto inboxRegistryDto,
            IAggregateClassifierDbEntityCondition<TId> aggregateClassifierDbEntityCondition,
            ITriggerEventRaiser triggerRaiser,
            Func<string, TId> idFactory,
            Func<MessageDto, AggregateDto> aggregateIdFactory,
            Func<MessageDto, string> idempotencyIdFactory
            )
        {
            _dbContext = dbContext;
            _inboxRegistryDto = inboxRegistryDto;
            _aggregateClassifierDbEntityCondition = aggregateClassifierDbEntityCondition;
            _triggerRaiser = triggerRaiser;
            _idFactory = idFactory;
            _aggregateIdFactory = aggregateIdFactory;
            _idempotencyIdFactory = idempotencyIdFactory;
        }

        public async ValueTask ProcessBatchAsync(
            ICollection<MessageDto> batch,
            CancellationToken cancellationToken)
        {
            // 1)
            var queue = await EFQueryHelper.GetOrInsertAsync(
                _dbContext,
                [batch.First().Queue],
                () => _dbContext.Set<QueueClassifierDbEntity<TId>>()
                    .AsNoTracking()
                    .Where(e => e.Name == batch.First().Queue),
                (e) => e.Name,
                (e) => new QueueClassifierDbEntity<TId>()
                {
                    Id = _idFactory(null),
                    Name = e,
                },
                cancellationToken
                );

            // 2)
            var messageByAggregate = batch
                .Select(e => (Message: e, AggregateId: _aggregateIdFactory(e)))
                .GroupBy(e => e.AggregateId)
                .ToArray();

            var aggregatesKeys = messageByAggregate
                .Select(e => e.Key)
                .ToDictionary(e => e, e => default(TId));

            var aggregates = await EFQueryHelper.GetOrInsertAsync(
                _dbContext,
                aggregatesKeys.Keys,
                () => _dbContext.Set<AggregateClassifierDbEntity<TId>>()
                    .AsNoTracking()
                    .ApplayQueryCondition(_aggregateClassifierDbEntityCondition.AggregateDto.QueryRange, (_dbContext, aggregatesKeys.Keys)),
                (e) => new AggregateDto(e.AggregateType, e.AggregateId),
                (e) => new AggregateClassifierDbEntity<TId>()
                {
                    Id = _idFactory(null),
                    AggregateType = e.AggregateType,
                    AggregateId = e.AggregateId,
                },
                cancellationToken
                );

            var aggregateIds = aggregates.Values.Select(e => e.Entity.Id).ToArray();

            // 3)
            var processDatas = await EFQueryHelper.GetOrInsertAsync(
                _dbContext,
                aggregates.Values.Select(e => e.Entity.Id).ToArray(),
                () => _dbContext.Set<InboxProcessDataDbEntity<TId>>()
                    .AsNoTracking()
                    .Where(e => aggregateIds.Contains(e.AggregateId)),
                (e) => e.AggregateId,
                (e) => new InboxProcessDataDbEntity<TId>()
                {
                    Id = _idFactory(null),
                    AggregateId = e,
                    QueueId = queue.Values.First().Entity.Id,
                },
                cancellationToken
                );

            // 3.1)
            var processes = processDatas.Values
                .Where(e => e.IsInserterted)
                .Select(
                    e => (
                        ProcessData: e.Entity,
                        Process: new ProcessDbEntity<TId>()
                        {
                            Id = _idFactory(null),
                            Error = new ProcessErrorDbEntity<TId>()
                            {
                                Id = _idFactory(null),
                                Error = null,
                            },
                            StoppedByError = false,
                            Priority = 0,
                            ProcessTypeId = _inboxRegistryDto.ProcessType.ProcessType,
                            ProcessVersion = _inboxRegistryDto.ProcessType.ProcessVersion,
                            RetryCount = null,
                            SelectLockTimeout = DateTimeOffset.MinValue.UtcDateTime,
                            Status = ProcessStatusEnum.WaitEvent,
                        }
                    )
                    )
                .ToArray();

            await _dbContext.Set<ProcessDbEntity<TId>>()
                .AddRangeAsync(
                    processes.Select(e => e.Process),
                    cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var elem in processes)
            {
                elem.ProcessData.ProcessId = elem.Process.Id;
                aggregatesKeys[new AggregateDto(elem.ProcessData.Aggregate.AggregateType, elem.ProcessData.Aggregate.AggregateId)] = elem.Process.Id;
            }

            // 4) Записываем сообщения и запускаем стрим.
            {
                var forInsert = new Dictionary<string, InboxMessageDbEntity<TId>>(batch.Count);
                foreach (var elem in messageByAggregate.SelectMany(e => e.Select(e2 => e2)))
                {
                    using var headersJson = JsonSerializer.SerializeToDocument(
                        elem.Message.Headers
                            .Select(e => new HeaderDto(e.key, e.value))
                            .ToArray()
                            );

                    forInsert.Add(
                        elem.Message.Key,
                        new InboxMessageDbEntity<TId>()
                        {
                            Id = _idFactory(null),
                            Key = elem.Message.Key,
                            ProcessId = aggregatesKeys[elem.AggregateId],
                            IdemporencyId = _idempotencyIdFactory(elem.Message),
                            Body = elem.Message.Body,
                            Headers = headersJson.RootElement.Clone(),
                            IsActive = true,
                            OrderId = DateTimeOffset.UtcNow.Nanosecond,
                            Partition = elem.Message.Partition,
                            Priority = 0,
                        }
                        );
                }

                // Обработка повторяющися сообщений(IdemporencyId)
                var result = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                    .UpsertRange(forInsert.Values)
                    .On(e => new { e.ProcessId, e.IdemporencyId })
                    .NoUpdate()
                    .RunAndReturnAsync(cancellationToken);
            }

            // Пробуждаем inbox процессы.
            await _triggerRaiser.RaiseAsync(
                messageByAggregate
                    .Select(
                        e => new TriggerEvent(
                            aggregatesKeys[e.Key].ToString(),
                            false
                            ))
                    .ToArray(),
                cancellationToken);
        }
    }
}
