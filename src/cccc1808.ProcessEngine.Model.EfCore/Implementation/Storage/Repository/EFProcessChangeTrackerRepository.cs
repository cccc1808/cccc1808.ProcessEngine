using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Common.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Dto.Components;
using cccc1808.ProcessEngine.Model.Implementation.Dto.Components;
using cccc1808.ProcessEngine.Model.Implementation.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage.Repository
{
    public class EFProcessChangeTrackerRepository<TId, TDbContext, TDbEntity>
        : IProcessRepository<TId>
        where TDbContext : DbContext
        where TDbEntity : ProcessDbEntity<TId>
    {
        protected readonly TDbContext _dbContext;
        protected readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IEnumerable<IProcessDbProvider<TId>> _processLoaders;
        private readonly ProcessIdDto_Id_Condition<TId, TDbEntity> _processIdDto_Id_Condition;
        private readonly IId_RangeCondition<TId, TDbEntity> _id_RangeCondition;
        private readonly Process_AsyncExecute_Condition<TId, TDbEntity> _process_AsyncExecute_Condition;

        public EFProcessChangeTrackerRepository(
            TDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IEnumerable<IProcessDbProvider<TId>> processLoaders)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _processLoaders = processLoaders;
            _processIdDto_Id_Condition = new ProcessIdDto_Id_Condition<TId, TDbEntity>();
            _id_RangeCondition = new IId_RangeCondition<TId, TDbEntity>();
            _process_AsyncExecute_Condition = new Process_AsyncExecute_Condition<TId, TDbEntity>();
        }

        public virtual async Task<ICollection<IProcessContainer<TId>>> GetRange(
            ICollection<ProcessIdDto<TId>> ids,
            bool withLock,
            CancellationToken cancellationToken)
        {
            TDbEntity[] data;
            using (var hint = _lockQueryHintStore.StartScope(withLock ? LockHintEnum.ForNoKeyUpdateAndSkipLocked : LockHintEnum.No))
            {
                data = await _dbContext.Set<TDbEntity>()
                    //.Include(e => e.Error)
                    .ApplayFilterCondition(
                        _id_RangeCondition,
                        ids.ApplayProjectionCondition(_processIdDto_Id_Condition).ToArray())
                    .ToArrayAsync(cancellationToken);
            }

            var containers = data.Select(
                e =>
                {
                    {
                        e.Error = new ProcessErrorDbEntity<TId>()
                        {
                            Id = e.Id,
                            Error = null
                        };
                        var entry = _dbContext.Attach(e.Error);
                        // entry.State = EntityState.Unchanged;
                    }

                    return new ProcessContainer<TId>(
                        new EFProcessProxyComponent<TId>(e),
                        new CurrentSessionComponent()
                        {
                            CreateRetryTimer = null,
                            HaveError = false,
                            IsSessionFirstStep = true,
                            ReTryLimit = 3,
                            RetryTimerCreated = false,
                            SessionId = Guid.Empty,
                            StopAsyncProcessingSession = false,
                        });
                })
                .ToDictionary(e => e.Process.Info.Id.Id, e => (IProcessContainer<TId>)e);

            var byTypeIndex = containers.Values
                .GroupBy(e => e.Process.Info.ProcessType)
                .ToDictionary(
                    e => e.Key, 
                    e => (ICollection<TId>)e.Select(e => e.Id).ToArray());
            foreach (var elem in _processLoaders)
            {
                await elem.LoadRangeAsync(
                    containers,
                    byTypeIndex,
                    withLock,
                    cancellationToken);
            }

            return containers.Values;
        }

        public virtual async Task<ICollection<IProcessContainer<TId>>> GetRangeForAsyncProcessingAsync(
            ICollection<ProcessIdDto<TId>> ids,
            CancellationToken cancellationToken)
        {
            TDbEntity[] data;
            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                data = await _dbContext.Set<TDbEntity>()
                    //.Include(e => e.Error)
                    .ApplayFilterCondition(_id_RangeCondition, ids.Select(e => e.Id).ToArray())
                    // Используем отдельный индекс.
                    .ApplayFilterCondition(_process_AsyncExecute_Condition, DateTimeOffset.UtcNow)
                    .ToArrayAsync(cancellationToken);
            }

            var containers = data.Select(
                e =>
                {
                    {
                        e.Error = new ProcessErrorDbEntity<TId>()
                        {
                            Id = e.Id,
                            Error = null
                        };
                        var entry = _dbContext.Attach(e.Error);
                        // entry.State = EntityState.Unchanged;
                    }

                    // Так как мы уже считали с блокировкой, то в конце текущей транзакции тожно сбросить SelectLock, т.к. сессия работы была завершена.
                    e.SelectLock = DateTimeOffset.MinValue.UtcDateTime;

                    return new ProcessContainer<TId>(
                        new EFProcessProxyComponent<TId>(e),
                        new CurrentSessionComponent()
                        {
                            CreateRetryTimer = null,
                            HaveError = false,
                            IsSessionFirstStep = true,
                            ReTryLimit = 3,
                            RetryTimerCreated = false,
                            SessionId = Guid.Empty
                        });
                })
                .ToDictionary(e => e.Process.Info.Id.Id, e => (IProcessContainer<TId>)e);

            var byTypeIndex = containers.Values
                .GroupBy(e => e.Process.Info.ProcessType)
                .ToDictionary(
                    e => e.Key,
                    e => (ICollection<TId>)e.Select(e => e.Id).ToArray());
            foreach (var elem in _processLoaders)
            {
                await elem.LoadForAsyncProcessingAsync(
                    containers,
                    byTypeIndex,
                    cancellationToken);
            }

            return containers.Values;
        }

        public virtual async Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var byTypeIndex = processes
                .GroupBy(e => e.Process.Info.ProcessType)
                .ToDictionary(
                    e => e.Key,
                    e => (ICollection<TId>)e.Select(e => e.Id).ToArray());

            foreach (var elem in _processLoaders)
            {
                await elem.UpdateAsync(
                    processes, 
                    byTypeIndex,
                    cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateWakeupAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
