using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.QueueModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.QueueModule.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.QueueModule.Provider
{
    public class EFDbQueueProducer<TId>
        : IQueueProducer
    {
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly EfDbQueueClassifier<TId> _classifier;

        public EFDbQueueProducer(
            IIdGenerator<TId> idGenerator,
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext,
            EfDbQueueClassifier<TId> classifier)
        {
            _idGenerator = idGenerator;
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _classifier = classifier;
        }

        public async Task ProduceBatchAsync(
            ICollection<MessageDto> messages, 
            CancellationToken cancellationToken)
        {
            foreach (var elem in messages)
            {
                // TODO: cccc1808/experiment/db_queue. выбор партиции.
                var partitionId = elem.Partition != -1
                    ? elem.Partition
                    : 0;

                _dbContext.Set<EFQueueMessageDbEntity<TId>>().Add(
                    new EFQueueMessageDbEntity<TId>(
                        await _idGenerator.NextAsync(cancellationToken),
                        await _classifier.GetOrCreateQueueParition(elem.Queue, elem.Partition, cancellationToken),
                        elem.Key,
                        offset: _dateTimeProvider.UtcNow.Ticks, // TODO: cccc1808/experiment/db_queue
                        System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone(), // TODO: cccc1808/experiment/db_queue.
                        elem.Body
                        )
                    );
            }

           await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
