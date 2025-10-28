using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Common.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
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
        private readonly ReTryTimerProcessRegistryDto _reTryTimerProcessRegistryDto;
        private readonly ProcessIdDto_Id_Condition<TId, TDbEntity> _processIdDto_Id_Condition;
        private readonly IId_RangeCondition<TId, TDbEntity> _id_RangeCondition;        
        private readonly Process_AsyncExecute_Condition<TId, TDbEntity> _process_AsyncExecute_Condition;

        public EFProcessChangeTrackerRepository(
            TDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IEnumerable<IProcessDbProvider<TId>> processLoaders,
            ReTryTimerProcessRegistryDto reTryTimerProcessRegistryDto)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _processLoaders = processLoaders;
            _processIdDto_Id_Condition = new ProcessIdDto_Id_Condition<TId, TDbEntity>();
            _id_RangeCondition = new IId_RangeCondition<TId, TDbEntity>();
            _process_AsyncExecute_Condition = new Process_AsyncExecute_Condition<TId, TDbEntity>();
            _reTryTimerProcessRegistryDto = reTryTimerProcessRegistryDto;
        }

        public virtual async Task<ICollection<IProcessContainer<TId>>> GetRangeWithLockAsync(
            ICollection<ProcessIdDto<TId>> ids,
            CancellationToken cancellationToken)
        {
            TDbEntity[] data;
            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
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
                        var entry = _dbContext.Attach(e);
                        entry.State = EntityState.Unchanged;
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
                            SessionId = Guid.Empty
                        });
                })
                .ToDictionary(e => e.Process.Info.Id.Id, e => (IProcessContainer<TId>)e);

            foreach (var elem in _processLoaders)
            {
                await elem.LoadForAsyncProcessingAsync(containers, cancellationToken);
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
                    .ApplayFilterCondition(_process_AsyncExecute_Condition, default)
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
                        var entry = _dbContext.Attach(e);
                        entry.State = EntityState.Unchanged;
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
                            SessionId = Guid.Empty
                        });
                })
                .ToDictionary(e => e.Process.Info.Id.Id, e => (IProcessContainer<TId>)e);

            foreach (var elem in _processLoaders)
            {
                await elem.LoadForAsyncProcessingAsync(containers, cancellationToken);
            }

            return containers.Values;
        }

        public virtual async Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            foreach (var elem in processes)
            {
                if (
                    elem.CurrentSession.CreateRetryTimer.HasValue
                    && !elem.CurrentSession.RetryTimerCreated)
                {
                    var timer = new TimerProcessDbEntity<TId>()
                    {
                        //TODO: Id
                        // Id = ,
                        ProcessTypeId = _reTryTimerProcessRegistryDto.ProcessType.ProcessType,
                        ProcessVersion = _reTryTimerProcessRegistryDto.ProcessType.ProcessVersion,
                        LinkedProcessId = elem.Id,
                        LinkedProcess = null,
                        IsProcessOrTimer = true,
                        HaveErrorFlag = false,
                        ReTryCount = null,
                        Priority = default,
                        SelectLock = DateTimeOffset.MinValue.UtcDateTime,
                        TimerDate = elem.CurrentSession.CreateRetryTimer.Value,
                        Error = new ProcessErrorDbEntity<TId>(),
                        Status = ProcessStatusEnum.AsyncExecute,
                    };

                    _dbContext.Add(timer);
                    elem.CurrentSession.RetryTimerCreated = true;
                }
            }

            foreach (var elem in _processLoaders)
            {
                await elem.UpdateAsync(processes, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
