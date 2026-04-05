using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Repository
{
    internal class EfTriggerRepository<TId> : ITriggerRepository<TId>
    {
        private readonly IEFDbContext _efDbContext;
        private readonly ILockQueryHintStore _lockQueryHintStore;

        private readonly ITriggerDbEntityConditions<TId> _triggerDbEntityConditions;

        private DbSet<TriggerDbEntity<TId>> Set => _efDbContext.Set<TriggerDbEntity<TId>>();

        public EfTriggerRepository(
            IEFDbContext efDbContext,
            ILockQueryHintStore lockQueryHintStore,

            ITriggerDbEntityConditions<TId> triggerDbEntityConditions)
        {
            _efDbContext = efDbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _triggerDbEntityConditions = triggerDbEntityConditions;
        }

        public async Task<IDictionary<string, ITriggerComponent<TId>>> LoadTriggerForQueueConsumerAsync(
            ICollection<string> keys,
            CancellationToken cancellationToken)
        {
            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
            {
                var data = await Set
                    .ApplayQueryCondition(_triggerDbEntityConditions.KeyAndNotComplete.QueryRange, keys)
                    .ToArrayAsync(cancellationToken);

                return data
                    .Select(e => new EFTriggerProxyComponent<TId>(e))
                    .ToDictionary(e => e.Key, e => (ITriggerComponent<TId>)e);
            }
        }

        public async Task<ICollection<ITriggerComponent<TId>>> LoadForHandlerAsync(
            ICollection<TId> ids,
            TimeSpan waitLockTimeout,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var result = await TimeoutHelper.ExecuteWithTimeoutAsync(
                (This: this, ids, now), 
                waitLockTimeout,
                static async (p, cancellationToken) => 
                {
                    using (var hint = p.This._lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                    {
                        var data = await p.This.Set
                            .ApplayQueryCondition(
                                p.This._triggerDbEntityConditions.DbProcessingForHandler.Query, 
                                new ITriggerDbEntityConditions<TId>.DbProcessingForHandlerParameters(
                                    p.now, 
                                    p.ids)
                                )
                            .ToArrayAsync(cancellationToken);

                        return data
                            .Select(e => (ITriggerComponent<TId>)new EFTriggerProxyComponent<TId>(e))
                            .ToArray();
                    }
                },
                cancellationToken
                ); 
            
            // Все блокировки получены.
            if (!result.IsTimeout)
            {
                return result.Result;
            }

            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var data = await Set
                    .ApplayQueryCondition(
                        _triggerDbEntityConditions.DbProcessingForHandler.Query,
                        new ITriggerDbEntityConditions<TId>.DbProcessingForHandlerParameters(
                            now,
                            ids)
                        ) // Для индекса.
                    .Where(e => ids.Contains(e.Id))
                    .ToArrayAsync(cancellationToken);

                return data
                    .Select(e => (ITriggerComponent<TId>)new EFTriggerProxyComponent<TId>(e))
                    .ToArray();
            }
        }

        public Task CreateTriggerAsync(
            string key,
            DateTimeOffset timerDate,
            TId processId,
            string handlerKey,
            ITriggerComponent<TId>.TriggerKind kind,
            short priority,
            bool isActivated,
            int? counter,
            CancellationToken cancellationToken)
        {
            if (key.Length > 255)
            {
                throw new ArgumentException(nameof(key));
            }

            var entity = new TriggerDbEntity<TId>(
                id: default,
                key: key,
                selectTimer: DateTimeOffset.MinValue,
                timerDate: timerDate,
                handlerKey: handlerKey,
                kind: kind,
                priority: priority,
                isActivated: isActivated,
                isCompleted: false,
                processId: processId,
                counter: counter
                );

            Set.Add(entity);
            return Task.CompletedTask;
        }

        public async Task SaveAsync(ICollection<ITriggerComponent<TId>> triggers, CancellationToken cancellationToken)
        {
            await _efDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
