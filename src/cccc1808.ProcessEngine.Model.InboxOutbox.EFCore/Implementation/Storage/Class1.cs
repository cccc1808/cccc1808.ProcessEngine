using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.Storage
{
    internal class Class1<TId, TDbContext>
        : IInboxOutboxRepository<TId>
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;

        public ValueTask<IDictionary<string, TId>> GetOrCreateAggregateIdRangeAsync(
            ICollection<string> name, 
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<IDictionary<TId, TId>> GetOrCreateInboxStreamByAggregateIdAsync(
            ICollection<(TId QueueId, TId AggregateId)> aggregateIds,
            CancellationToken cancellationToken)
        {
            var founded = new Dictionary<(TId QueueId, TId AggregateId), TId>(aggregateIds.Count);
            var notFounded = new HashSet<(TId QueueId, TId AggregateId)>(aggregateIds.Count);
            {
                var inboxStreamData = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
                    .Where(e => aggregateIds.Contains((e.QueueId, e.AggregateId)))
                    .ToDictionaryAsync(e => e.AggregateId, e => e.Id, cancellationToken);

                foreach (var elem in aggregateIds)
                {
                    if (inboxStreamData.TryGetValue(elem, out var streamId))
                    {
                        founded.Add(elem, streamId);
                    }
                    else
                    {
                        notFounded.Add(elem);
                    }
                }

                if (notFounded.Count == 0)
                {
                    return founded;
                }
            }

            {
                var createdStreams = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
                    .UpsertRange(
                        notFounded
                            .Select(
                                e => new InboxProcessDataDbEntity<TId>() 
                                {
                                    Id = default,
                                    AggregateId = e.AggregateId,
                                    Aggregate = null,
                                    QueueId = e.QueueId,
                                    Queue = null,                                    
                                }
                                )
                            .ToArray()                    
                        )
                    .RunAndReturnAsync(cancellationToken);

                dbContext.Set<TimerProcessDbEntity<TId>>()
                    .Add(
                        new TimerProcessDbEntity<TId>()
                        {
                            Id = inboxStreamData.Id,
                            Error = new ProcessErrorDbEntity<TId>()
                            {
                                Id = inboxStreamData.Id,
                                Error = null,
                            },
                            HaveErrorFlag = false,
                            IsProcessOrTimer = false,
                            LinkedProcess = null,
                            LinkedProcessId = default,
                            Priority = 0,
                            ProcessTypeId = inboxRegistry.ProcessType.ProcessType,
                            ProcessVersion = inboxRegistry.ProcessType.ProcessVersion,
                            ReTryCount = null,
                            SelectLock = DateTimeOffset.MinValue.UtcDateTime,
                            Status = Model.Abstract.Dto.ProcessStatusEnum.WaitEvent,
                            TimerDate = DateTimeOffset.MinValue.UtcDateTime,
                        }
                    );

            }

            var data = aggregateIds
                .Select(e => (e, , aggregateId))
                .ToDictionary(e => e.e, e => ());

            if (inboxStreamData == null)
            {
                // Создаем стрим, если его нет.
                await using (var transaction = await transactionManager.StartTransactionAsync(cancelationToken))
                {
                    

                    inboxStreamData = await dbContext.Set<InboxProcessDataDbEntity<TId>>()
                        .AsNoTracking()
                        .FirstAsync(e => e.AggregateId == elem.Key, cancelationToken);
                    aggregateIdCache.Add(elem.Key, inboxStreamData.Id);

                    if (result == 1)
                    {
                        dbContext.Set<TimerProcessDbEntity<TId>>()
                            .Add(
                                new TimerProcessDbEntity<TId>()
                                {
                                    Id = inboxStreamData.Id,
                                    Error = new ProcessErrorDbEntity<TId>()
                                    {
                                        Id = inboxStreamData.Id,
                                        Error = null,
                                    },
                                    HaveErrorFlag = false,
                                    IsProcessOrTimer = false,
                                    LinkedProcess = null,
                                    LinkedProcessId = default,
                                    Priority = 0,
                                    ProcessTypeId = inboxRegistry.ProcessType.ProcessType,
                                    ProcessVersion = inboxRegistry.ProcessType.ProcessVersion,
                                    ReTryCount = null,
                                    SelectLock = DateTimeOffset.MinValue.UtcDateTime,
                                    Status = Model.Abstract.Dto.ProcessStatusEnum.WaitEvent,
                                    TimerDate = DateTimeOffset.MinValue.UtcDateTime,
                                }
                            );

                        await dbContext.SaveChangesAsync(cancelationToken);
                    }

                    throw new NotImplementedException();
        }

        public ValueTask<IDictionary<TId, TId>> GetOrCreateOutboxStreamByAggregateIdAsync(ICollection<TId> aggregateIds)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<TId> GetOrCreateQueueIdAsync(
            string name, 
            CancellationToken cancellationToken)
        {
            var queue = await _dbContext.Set<QueueClassifierDbEntity<TId>>()
                .AsNoTracking()
                .Where(e => e.Name == name)
                .FirstOrDefaultAsync(cancellationToken);

            if (queue == null)
            {
                await _dbContext.Set<QueueClassifierDbEntity<TId>>()
                    .Upsert(new QueueClassifierDbEntity<TId>() { Id = default, Name = name })
                    .RunAsync(cancellationToken);
            }

            queue = await _dbContext.Set<QueueClassifierDbEntity<TId>>()
                .AsNoTracking()
                .Where(e => e.Name == name)
                .FirstAsync(cancellationToken);

            return queue.Id;
        }

        public ValueTask SendMessagesAsync(IDictionary<TId, ICollection<MessageDto>> messagesByStreams, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
