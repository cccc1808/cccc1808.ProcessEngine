using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.QueueModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.QueueModule.Provider
{
    public class EFDbQueueConsumer<TId> : IQueueConsumer
    {
        private readonly IEFDbContext _dbContext;
        private readonly ILockQueryHintStore _lockQueryHintStore;

        private readonly OptionsDto _options;


        public EFDbQueueConsumer(
            IEFDbContext dbContext, 
            ILockQueryHintStore lockQueryHintStore, 

            OptionsDto options)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;

            _options = options;
        }

        public async ValueTask<ICollection<MessageDto>> ConsumeBatchAsync(
            int limit,
            TimeSpan batchTimeout,
            CancellationToken cancellationToken)
        {
            (EFQueuePartitionDbEntity<TId> QueueTopic, EFQueueMessageDbEntity<TId> Message)[] data;
            using (var scope = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                // TODO: cccc1808/experiment/db_queue. Индексы.
                var d = await _dbContext.Set<EFQueuePartitionDbEntity<TId>>()
                    // .AsNoTracking()
                    .Join(
                        _dbContext.Set<EFQueueMessageDbEntity<TId>>(),
                        e => e.Id,
                        e => e.QueuePartitionId,
                        (e1, e2) => new { QueueTopic = e1, Message = e2 })
                    .OrderBy(e => e.QueueTopic.ProcessDate) // Берем самый старый по дате обработке.
                    .ThenBy(e => e.Message.Offset)
                    .Take(limit)
                    .ToArrayAsync(cancellationToken);

                if (!d.Any())
                {
                    await Task.Delay(
                        TimespanHelper.Min(_options.EmptyTimeout, batchTimeout),
                        cancellationToken
                        );
                }

                data = d
                    .Select(e => (e.QueueTopic, e.Message))
                    .ToArray();
            }

            // Удаление - сообщение обработано. Привязано к db transaction.
            // _dbContext.Set<EFQueueMessageDbEntity<TId>>().RemoveRange(data.Select(e => e.Message));

            await _dbContext.Set<EFQueueMessageDbEntity<TId>>()
                .Where(e => data.Select(e => e.Message.Id).Contains(e.Id))
                .ExecuteDeleteAsync(cancellationToken);

            var result = data
                .Select(e => Map(e.Message, e.QueueTopic.PartitionId))
                .ToArray();

            await _dbContext.SaveChangesAsync(cancellationToken);
            return result;
        }

        public async ValueTask ConsumeBatchAsync<TParameter>(
            TParameter parameter, 
            TimeSpan batchTimeout,
            Func<TParameter, MessageDto, bool> onReceivedHandler,
            CancellationToken cancellationToken)
        {
            List<EFQueueMessageDbEntity<TId>> forRemove;
            using (var scope = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var d = await _dbContext.Set<EFQueuePartitionDbEntity<TId>>()
                    // .AsNoTracking()
                    .Join(
                        _dbContext.Set<EFQueueMessageDbEntity<TId>>(),
                        e => e.Id,
                        e => e.QueuePartitionId,
                        (e1, e2) => new { QueueTopic = e1, Message = e2 })
                    .OrderBy(e => e.QueueTopic.ProcessDate) // Берем самый старый по дате обработк и partition.
                    .ThenBy(e => e.Message.Offset)
                    .Take(_options.PackLimit)
                    .ToArrayAsync(cancellationToken);

                if (!d.Any())
                {
                    await Task.Delay(
                        TimespanHelper.Min(_options.EmptyTimeout, batchTimeout),
                        cancellationToken
                        );
                }

                forRemove = new List<EFQueueMessageDbEntity<TId>>(d.Length);

                foreach (var elem in d)
                {
                    var message = Map(elem.Message, elem.QueueTopic.PartitionId);
                    forRemove.Add(elem.Message);

                    if (!onReceivedHandler(parameter, message))
                    {
                        break;                        
                    }
                }
            }

            // Удаление - сообщение обработано. Привязано к db transaction.
            // _dbContext.Set<EFQueueMessageDbEntity<TId>>().RemoveRange(forRemove);

            await _dbContext.Set<EFQueueMessageDbEntity<TId>>()
                .Where(e => forRemove.Select(e => e.Id).Contains(e.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            // Замечание удаляем сразу при consume т.к. завязываемся на Db transaction scope.
            return ValueTask.CompletedTask;
        }

        private MessageDto Map(
            EFQueueMessageDbEntity<TId> source,
            int partitionId)
        {
            return new MessageDto(
                source.Key,
                _options.QueueName,
                null, // TODO: cccc1808/experiment/db_queue.
                source.Body,
                partitionId);
        }

        public class OptionsDto
        {
            public string QueueName { get; set; }

            public TimeSpan EmptyTimeout { get; set; }
                = TimeSpan.FromSeconds(0.1);

            public int PackLimit { get; set; }
                = 100;

            public OptionsDto(string queueName) 
            {
                QueueName = queueName;
            }

            public OptionsDto(
                string queueName, 
                TimeSpan emptyTimeout, 
                int packLimit)
            {
                QueueName = queueName;
                EmptyTimeout = emptyTimeout;
                PackLimit = packLimit;
            }            
        }
    }
}
