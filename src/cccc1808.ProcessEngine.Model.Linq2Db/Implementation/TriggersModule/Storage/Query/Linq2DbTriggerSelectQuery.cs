using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Async;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query
{
    public class Linq2DbTriggerSelectQuery<TId> : ITriggerSelectQuery<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILinq2DbDataConnection _dataConnection;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITriggerHandlerFactory<TId> _triggerHandlerFactory;

        private readonly ITriggerDbEntityConditions<TId> _triggerDbEntityConditions;

        public Linq2DbTriggerSelectQuery(
            IServiceProvider serviceProvider,
            ILinq2DbDataConnection dataConnection,
            IDateTimeProvider dateTimeProvider,
            ITriggerHandlerFactory<TId> triggerHandlerFactory,

            ITriggerDbEntityConditions<TId> triggerDbEntityConditions)
        {
            _serviceProvider = serviceProvider;
            _dataConnection = dataConnection;
            _dateTimeProvider = dateTimeProvider;
            _triggerHandlerFactory = triggerHandlerFactory;

            _triggerDbEntityConditions = triggerDbEntityConditions;
        }

        public async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> SelectForProcessingAsync(
            int batchSize,
            int parallelLimit,
            int transactionUpdateLimit,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return await Implementation2Async(
                batchSize,
                parallelLimit,
                transactionUpdateLimit,
                timeout,
                cancellationToken);
        }

        private async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> Implementation1Async(
            int batchSize,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            var selectLockValue = now + timeout;

            var data = await _dataConnection.Set<TriggerDbEntity<TId>>()
                .ApplayQueryCondition(
                    _triggerDbEntityConditions.DbProcessingForSelector2.Query,
                    new ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters(
                        now)
                    )
                .Take(batchSize)
                .Select(e => new { e.Id, e.HandlerKey })
                .ToArrayAsync(cancellationToken);

            await _dataConnection.Set<TriggerDbEntity<TId>>()
                .Where(e => data.Select(e => e.Id).Contains(e.Id))
                .Set(e => e.SelectLockTimeout, selectLockValue)
                .UpdateAsync(cancellationToken);

            var result = data
                .Select(e => new ITriggerSelectQuery<TId>.SelectDto(e.Id, e.HandlerKey))
                .ToArray();

            return result;
        }

        private async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> Implementation2Async(
            int batchSize,
            int parallelLimit,
            int transactionUpdateLimit,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            var selectLockValue = now + timeout;
            var result = new List<ITriggerSelectQuery<TId>.SelectDto>(batchSize);

            {
                var data = await _dataConnection.Set<TriggerDbEntity<TId>>()
                .ApplayQueryCondition(
                    _triggerDbEntityConditions.DbProcessingForSelector2.Query,
                    new ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters(
                        now)
                    )
                .QueryHint(PostgresQueryHint.ForNoKeyUpdateSkipLocked)
                .Take(batchSize)
                .Select(e => new { e.Id, e.HandlerKey })
                .ToArrayAsync(cancellationToken);

                // Ограничиваем по количетсву параллельных слотов в зависимости от типа триггера.
                var parallelCounter = 0;
                var rangeTriggerGroups = new Dictionary<string, int>(parallelLimit);
                foreach (var elem in data)
                {
                    var handler = _triggerHandlerFactory.GetHandler(_serviceProvider, elem.HandlerKey);
                    var elemResult = new ITriggerSelectQuery<TId>.SelectDto(elem.Id, elem.HandlerKey);

                    switch (handler)
                    {
                        case ITriggerRangeHandler<TId> rangeTrigger:
                            {
                                // Ограничение 
                                if (rangeTriggerGroups.TryGetValue(elem.HandlerKey, out var value))
                                {
                                    result.Add(elemResult);

                                    if (value < transactionUpdateLimit)
                                    {
                                        // Общая транзакция
                                        rangeTriggerGroups[elem.HandlerKey] = value + 1;
                                    }
                                    else
                                    {
                                        // Новая транзакция (parallel slot)
                                        rangeTriggerGroups[elem.HandlerKey] = 1;
                                        parallelCounter++;
                                    }
                                }
                                else
                                {
                                    // Новая транзакция (parallel slot)
                                    result.Add(elemResult);
                                    rangeTriggerGroups.Add(elem.HandlerKey, 1);
                                    parallelCounter++;
                                }

                                break;
                            }

                        case ITriggerSingleHandler<TId> singleHandler:
                            {
                                result.Add(elemResult);
                                parallelCounter++;
                                break;
                            }
                    }

                    // Все слоты заняты.
                    if (parallelCounter == parallelLimit)
                    {
                        break;
                    }
                }
            }

            await _dataConnection.Set<TriggerDbEntity<TId>>()
                .Where(e => result.Select(e => e.Id).Contains(e.Id))
                .Set(e => e.SelectLockTimeout, selectLockValue)
                .UpdateAsync(cancellationToken);


            return result;
        }
    }
}
