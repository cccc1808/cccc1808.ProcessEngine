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
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
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

        public async Task<TId?> GetMinIdAsync(CancellationToken cancellationToken)
        {
            var result = await _dbContext.Set<TriggerDbEntity<TId>>()
                .OrderBy(e => e.Id)
                .Select(e => new { e.Id })
                .FirstOrDefaultAsync(cancellationToken);

            return result is not null 
                ? result.Id 
                : default;
        }

        public async Task<BitFlagDto> GetProcessSignalFilterAsync(
            TId processId,
            CancellationToken cancellationToken)
        {
            var result = await _dbContext.Set<ProcessDbEntity<TId>>()
                .Where(e => e.Id.Equals(processId))
                .Select(e => e.SignalCodeFilter)
                .FirstAsync(cancellationToken);

            return new BitFlagDto(result);
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

        public async Task<IDictionary<string, EmergencyTriggerHandler<TId>.IQueries.StatusInfo>> LoadAsync(
            int batchSize, 
            TId offsetId,
            CancellationToken cancellationToken)
        {
            var triggerData = await _dbContext.Set<TriggerDbEntity<TId>>()
                .Where(e =>
                    e.IsRangeHandler // Триггеры обработчики только с таким типом.
                    && Comparer<TId>.Default.Compare(e.Id, offsetId) > 0 // keyset
                    )
                .Take(batchSize)
                .ToArrayAsync(cancellationToken);

            // [MVCC Only]: т.к. тут чтение не конкурирует не с какими блокировками. Иначе подумать.
            var processData = await _dbContext.Set<ProcessDbEntity<TId>>()
                .Where(e => triggerData.Select(e => e.ProcessId).Contains(e.Id))
                .Select(e => new { e.Id, e.Status, e.ReservationTimeout })
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            return triggerData.ToDictionary(
                e => e.Key,
                (Func<TriggerDbEntity<TId>, EmergencyTriggerHandler<TId>.IQueries.StatusInfo>)(                e => 
                {
                    if (processData.TryGetValue(e.ProcessId, out var process))
                    {
                        return new EmergencyTriggerHandler<TId>.IQueries.StatusInfo(
                            e.Id,
                            (ITriggerComponent<TId>)new EFTriggerProxyComponent<TId>(_triggerSetter, e),
                            ProcessDeleted: false,
                            ProcessStatus: process.Status,
                            ReservationTimeout: process.ReservationTimeout
                            );
                    }
                    else 
                    {
                        return new EmergencyTriggerHandler<TId>.IQueries.StatusInfo(
                            e.Id,
                            new EFTriggerProxyComponent<TId>(_triggerSetter, e),
                            ProcessDeleted: true,
                            ProcessStatus: null,
                            ReservationTimeout: null
                            );
                    }
                })
                );
        }

        public async Task<HashSet<string>> LockSkipLockedAsync(
            ICollection<string> triggersKeys,
            CancellationToken cancellationToken)
        {
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var data = await _dbContext.Set<TriggerDbEntity<TId>>()
                    .Where(e => triggersKeys.Contains(e.Key))
                    .Select(e => e.Key)
                    .ToHashSetAsync(cancellationToken);

                return data;
            }
        }
    }
}
