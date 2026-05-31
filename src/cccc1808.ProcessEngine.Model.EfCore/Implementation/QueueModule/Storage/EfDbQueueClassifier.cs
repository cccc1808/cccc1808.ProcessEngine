using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.QueueModule.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.QueueModule.Storage
{
    public class EfDbQueueClassifier<TId>
    {
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IEFDbContext _dbContext;
        private readonly CacheState _cache;

        public EfDbQueueClassifier(
            IIdGenerator<TId> idGenerator, 
            IEFDbContext dbContext,
            CacheState cache)
        {
            _idGenerator = idGenerator;
            _dbContext = dbContext;
            _cache = cache;
        }

        public async ValueTask<TId> GetOrCreateQueueParition(
            string name, 
            int partitionId, 
            CancellationToken cancellationToken)
        {
            if (_cache.QueuePartitionCache.TryGetValue((name, partitionId), out var value))
            {
                return value;
            }

            var result = await _dbContext.Set<EFQueuePartitionDbEntity<TId>>()
                .Upsert(new EFQueuePartitionDbEntity<TId>(
                    await _idGenerator.NextAsync(cancellationToken),
                    name, 
                    partitionId, 
                    DateTimeOffset.MinValue))
                .NoUpdate()
                .RunAndReturnAsync(cancellationToken);

            var dbValue = result.FirstOrDefault();
            if (dbValue == null)
            {
                dbValue = await _dbContext.Set<EFQueuePartitionDbEntity<TId>>()
                    .AsNoTracking()
                    .FirstAsync(e => e.TopicName == name && e.PartitionId == partitionId, cancellationToken);
            }

            _cache.QueuePartitionCache.TryAdd((name, partitionId), dbValue.Id);
            return dbValue.Id;
        }

        // TODO: cccc1808/experiment/db_queue
        public class CacheState
        {
            public ConcurrentDictionary<(string, int), TId> QueuePartitionCache { get; }
                = new ConcurrentDictionary<(string, int), TId>();
        }
    }
}
