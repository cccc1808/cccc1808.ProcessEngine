using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.OutboxModule.Services;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage
{
    public class EFOutboxRangeProcess2Queries<TId>
        : OutboxRangeProcessHandler2<TId>.IQueries
    {
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IEFDbContext _dbContext;        
        private readonly IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFOutboxRangeProcess2Queries(
            ILockQueryHintStore lockQueryHintStore,
            IEFDbContext dbContext, 
            IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> messageStreamConditions)
        {
            _lockQueryHintStore = lockQueryHintStore;
            _dbContext = dbContext;
            _messageStreamConditions = messageStreamConditions;
        }

        public async Task<IOutboxMessageComponent<TId>[]> LoadMessagesForProcessingAsync(
            ICollection<TId> processIds,
            int batchSize,
            CancellationToken cancellationToken)
        {
            using (var scope = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var messages = await _dbContext.Set<OutboxMessageDbEntity<TId>>()
                    .ApplayQueryCondition(
                        _messageStreamConditions.ForProcessing.QueryIds,
                        new IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>>.ForProcessingParamDto2(processIds))
                    .Take(batchSize)
                    .ToArrayAsync(cancellationToken);

                return messages
                    .Select(e => (IOutboxMessageComponent<TId>)new EFOutboxMessageProxy<TId>(e))
                    .ToArray();
            }
        }

        public async Task<HashSet<TId>> NotProcessedMessagesExsistsAsync(
            ICollection<TId> processIds,
            CancellationToken cancellationToken)
        {
            var result = await _dbContext.Set<OutboxMessageDbEntity<TId>>()
                .ApplayQueryCondition(
                    _messageStreamConditions.ForProcessing.QueryIds,
                    new IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>>.ForProcessingParamDto2(processIds)
                    )
                .GroupBy(e => e.ProcessId)
                .Select(e => e.Key)
                .ToHashSetAsync(cancellationToken);

            return result;
        }

        public async Task UpdateMessagesAsync(
            IOutboxMessageComponent<TId>[] messages, 
            CancellationToken cancellationToken)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
