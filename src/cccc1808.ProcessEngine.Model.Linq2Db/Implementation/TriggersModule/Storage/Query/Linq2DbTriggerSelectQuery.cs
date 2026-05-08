using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB.Async;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query
{
    public class Linq2DbTriggerSelectQuery<TId> : ITriggerSelectQuery<TId>
    {
        private readonly ILinq2DbDataConnection _dataConnection;
        private readonly IDateTimeProvider _dateTimeProvider;

        private readonly ITriggerDbEntityConditions<TId> _triggerDbEntityConditions;

        public Linq2DbTriggerSelectQuery(
            ILinq2DbDataConnection dataConnection,
            IDateTimeProvider dateTimeProvider,

            ITriggerDbEntityConditions<TId> triggerDbEntityConditions)
        {
            _dataConnection = dataConnection;
            _dateTimeProvider = dateTimeProvider;

            _triggerDbEntityConditions = triggerDbEntityConditions;
        }

        public async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> SelectForProcessingAsync(
            int batchSize, 
            TimeSpan timeout, 
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            ITriggerSelectQuery<TId>.SelectDto[] result;

            {
                var data = await _dataConnection.Set<TriggerDbEntity<TId>>()
                    .ApplayQueryCondition(
                        _triggerDbEntityConditions.DbProcessingForSelector.Query,
                        new ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters(
                            now)
                        )
                    .Take(batchSize)
                    .Select(e => new { e.Id, e.HandlerKey })
                    .ToArrayAsync(cancellationToken);

                result = data
                    .Select(e => new ITriggerSelectQuery<TId>.SelectDto(e.Id, e.HandlerKey))
                    .ToArray();
            }

            return result;
        }
    }
}
