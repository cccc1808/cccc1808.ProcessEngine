using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query
{
    public class EFTriggerSelectQuery<TId> : ITriggerSelectQuery<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILockQueryHintStore _lockQueryHintStore;

        private readonly ITriggerDbEntityConditions<TId> _triggerDbEntityConditions;

        public EFTriggerSelectQuery(
            IEFDbContext dbContext,
            IDateTimeProvider dateTimeProvider,
            ILockQueryHintStore lockQueryHintStore,

            ITriggerDbEntityConditions<TId> triggerDbEntityConditions)
        {
            _dbContext = dbContext;
            _dateTimeProvider = dateTimeProvider;
            _lockQueryHintStore = lockQueryHintStore;

            _triggerDbEntityConditions = triggerDbEntityConditions;
        }

        public async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> SelectForProcessingAsync(
            int batchSize, 
            TimeSpan timeout, 
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            ITriggerSelectQuery<TId>.SelectDto[] result;
            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var data = await _dbContext.Set<TriggerDbEntity<TId>>()
                    .AsNoTracking()
                    .ApplayQueryCondition(
                        _triggerDbEntityConditions.DbProcessingForSelector.Query, 
                        new ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters(
                            now))
                    .Take(batchSize)
                    .Select(e => new { e.Id, e.HandlerKey })
                    .ToArrayAsync(cancellationToken);

                result = data
                    .Select(e => new ITriggerSelectQuery<TId>.SelectDto(e.Id, e.HandlerKey))
                    .ToArray();
            }

            var ids = result
                .Select(e => e.Id)
                .ToArray();

            await _dbContext.Set<TriggerDbEntity<TId>>()
                .ApplayQueryCondition(
                    _triggerDbEntityConditions.DbProcessingForHandler.Query,
                    new ITriggerDbEntityConditions<TId>.DbProcessingForHandlerParameters(
                        now,
                        ids
                        )
                    )
                .ExecuteUpdateAsync(e => e.SetProperty(e => e.SelectLockTimeout, _dateTimeProvider.UtcNow + timeout), cancellationToken);

            return result;
        }
    }
}
