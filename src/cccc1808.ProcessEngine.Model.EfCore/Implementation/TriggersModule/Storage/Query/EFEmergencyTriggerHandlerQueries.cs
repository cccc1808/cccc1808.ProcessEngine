using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query
{
    public class EFEmergencyTriggerHandlerQueries<TId>
        : EmergencyTriggerHandler<TId>.IQueries
    {
        private readonly IEFDbContext _dbContext;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly ITriggerSetter<TId> _triggerSetter;

        public EFEmergencyTriggerHandlerQueries(
            IEFDbContext dbContext, 
            ILockQueryHintStore lockQueryHintStore, 
            ITriggerSetter<TId> triggerSetter)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _triggerSetter = triggerSetter;
        }

        public async Task<ICollection<ITriggerComponent<TId>>> LoadAsync(
            ISet<string> ignoreHandlers, 
            DateTimeOffset timeout,
            int batchSize,
            CancellationToken cancellationToken)
        {
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var data = await _dbContext.Set<TriggerDbEntity<TId>>()
                    .Where(e =>
                        !e.IsCompleted
                        && !e.IsActivated
                        && e.Kind != ITriggerComponent.TriggerKind.SimpleStreamRoot // Игнорируем корневые триггеры.
                        && e.IsRangeHandler // Триггеры обработчики только с таким типом.
                        && e.SelectLockTimeout < timeout // Давно не брался в обработку.
                        && !ignoreHandlers.Contains(e.HandlerKey)
                        )
                    .Take(batchSize)
                    .ToArrayAsync(cancellationToken);

                return data
                    .Select(e => new EFTriggerProxyComponent<TId>(_triggerSetter, e))
                    .ToArray();
            }
        }
    }
}
