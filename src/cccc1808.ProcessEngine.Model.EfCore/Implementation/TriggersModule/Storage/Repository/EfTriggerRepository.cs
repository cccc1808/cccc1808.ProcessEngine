using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Repository
{
    public class EfTriggerRepository<TId> : ITriggerRepository<TId>
    {
        private readonly IEFDbContext _efDbContext;
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITriggerSetter<TId> _triggerSetter;
        private readonly ILockQueryHintStore _lockQueryHintStore;

        private readonly ITriggerDbEntityConditions<TId> _triggerDbEntityConditions;

        private DbSet<TriggerDbEntity<TId>> Set => _efDbContext.Set<TriggerDbEntity<TId>>();

        public EfTriggerRepository(
            IEFDbContext efDbContext,
            IIdGenerator<TId> idGenerator,
            IDateTimeProvider dateTimeProvider,
            ITriggerSetter<TId> triggerSetter,
            ILockQueryHintStore lockQueryHintStore,

            ITriggerDbEntityConditions<TId> triggerDbEntityConditions)
        {
            _efDbContext = efDbContext;
            _idGenerator = idGenerator;
            _dateTimeProvider = dateTimeProvider;
            _lockQueryHintStore = lockQueryHintStore;
            _triggerSetter = triggerSetter;
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
                    .Select(e => (ITriggerComponent<TId>)new EFTriggerProxyComponent<TId>(_triggerSetter, e))
                    .ToDictionary(e => e.Key, e => e);
            }
        }

        public async Task<ICollection<ITriggerComponent<TId>>> LoadForHandlerAsync(
            ICollection<TId> ids,
            TimeSpan waitLockTimeout,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
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
                            .Select(e => (ITriggerComponent<TId>)new EFTriggerProxyComponent<TId>(p.This._triggerSetter, e))
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
                        )
                    .ToArrayAsync(cancellationToken);

                return data
                    .Select(e => (ITriggerComponent<TId>)new EFTriggerProxyComponent<TId>(_triggerSetter, e))
                    .ToArray();
            }
        }

        public async Task CreateTriggerAsync(
            ITriggerRepository<TId>.CreateTriggerDto createDto,
            CancellationToken cancellationToken)
        {
            await CreateTriggerRangeAsync(
                [createDto], 
                cancellationToken);
        }

        public async Task CreateTriggerRangeAsync(
            ICollection<ITriggerRepository<TId>.CreateTriggerDto> createDto,
            CancellationToken cancellationToken)
        {
            var create = new List<TriggerDbEntity<TId>>(createDto.Count);
            foreach (var elem in createDto)
            {
                if (elem.key.Length > 255)
                {
                    throw new ArgumentException(nameof(elem.key));
                }
                if (elem.handlerKey.Length > 255)
                {
                    throw new ArgumentException(nameof(elem.handlerKey));
                }

                create.Add(new TriggerDbEntity<TId>(
                    id: await _idGenerator.NextAsync(cancellationToken),
                    key: elem.key,
                    selectLockTimeout: DateTimeOffset.MinValue,
                    timerDate: elem.timerDate,
                    isRangeHandler: elem.isRangeTrigger,
                    handlerKey: elem.handlerKey,
                    kind: elem.kind,
                    priority: elem.priority,
                    isActivated: elem.isActivated,
                    isCompleted: false,
                    processId: elem.processId,
                    streamProcessIsWaiting: elem.streamProcessIsWaiting,
                    signalCounter1: elem.signalCounter1,
                    signalCounter2: elem.signalCounter2,
                    isChildTrigger: elem.isChildTrigger,
                    offsetId: default // Заполняется только при обработке, на создании - null.
                    ));
            }

            Set.AddRange(create);
        }

        public async Task SaveAsync(ICollection<ITriggerComponent<TId>> triggers, CancellationToken cancellationToken)
        {
            var forRemove = new List<TriggerDbEntity<TId>>();
            foreach (var elem in triggers)
            {
                if (elem is not EFTriggerProxyComponent<TId> proxy)
                {
                    throw new ArgumentException($"Ожидается {nameof(EFTriggerProxyComponent<TId>)}");
                }

                if (elem.NeedRemove)
                {
                    forRemove.Add(proxy.Entity);
                }
            }
            Set.RemoveRange(forRemove);
            await _efDbContext.SaveChangesAsync(cancellationToken);

            foreach (var elem in triggers)
            {
                elem.NeedUpdate = false;
                elem.NeedRemove = false;
            }
        }

        public async Task<HashSet<TId>> CheckProcessWaitingAsync(
            ICollection<TId> processIds, 
            CancellationToken cancellationToken)
        {
            return await _efDbContext.Set<ProcessDbEntity<TId>>()
                .Where(e => 
                    processIds.Contains(e.Id) 
                    && e.Status == ProcessStatusEnum.WaitEvent)
                .Select(e => e.Id)
                .ToHashSetAsync(cancellationToken);
        }
    }
}
