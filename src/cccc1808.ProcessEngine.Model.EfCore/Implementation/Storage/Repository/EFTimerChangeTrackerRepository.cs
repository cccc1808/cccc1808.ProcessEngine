//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using cccc1808.ProcessEngine.Model.Abstract.Dto;
//using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
//using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions;
//using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
//using cccc1808.ProcessEngine.Model.Common.Condition;
//using cccc1808.ProcessEngine.Model.Common.Entities.Conditions;
//using cccc1808.ProcessEngine.Model.Common.QueryHint;
//using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
//using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions;
//using cccc1808.ProcessEngine.Model.EfCore.Implementation.Dto.Components;
//using cccc1808.ProcessEngine.Model.Implementation.Dto.Components;
//using cccc1808.ProcessEngine.Model.Implementation.Storage;

//using Microsoft.EntityFrameworkCore;

//namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage.Repository
//{
//    public class EFTimerChangeTrackerRepository<TId, TDbContext, TTimerProcessEntity>
//        : IProcessRepository<TId>         
//        where TDbContext : DbContext         
//        where TTimerProcessEntity : TimerProcessDbEntity<TId>      
//    {
//        protected readonly TDbContext _dbContext;
//        protected readonly ILockQueryHintStore _lockQueryHintStore;
//        private readonly IEnumerable<IProcessDbProvider<TId>> _processLoaders;
//        private readonly ProcessIdDto_Id_Condition<TId, TTimerProcessEntity> _processIdDto_Id_Condition;
//        private readonly IId_RangeCondition<TId, TTimerProcessEntity> _timer_id_RangeCondition;
        
//        private readonly Process_AsyncExecute_Condition<TId, TTimerProcessEntity> _process_AsyncExecute_Condition;

//        public EFTimerChangeTrackerRepository(
//            TDbContext dbContext,
//            ILockQueryHintStore lockQueryHintStore,
//            IEnumerable<IProcessDbProvider<TId>> processLoaders)
//        {
//            _dbContext = dbContext;
//            _lockQueryHintStore = lockQueryHintStore;
//            _processLoaders = processLoaders;
//            _processIdDto_Id_Condition = new ProcessIdDto_Id_Condition<TId, TTimerProcessEntity>();
//            _timer_id_RangeCondition = new IId_RangeCondition<TId, TTimerProcessEntity>();
//            _process_AsyncExecute_Condition = new Process_AsyncExecute_Condition<TId, TTimerProcessEntity>();           
//        }

//        public virtual async Task<ICollection<IProcessContainer<TId>>> GetRangeWithLockAsync(
//            ICollection<ProcessIdDto<TId>> ids,
//            CancellationToken cancellationToken)
//        {
//            TTimerProcessEntity[] data;
//            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
//            {
//                data = await _dbContext.Set<TTimerProcessEntity>()                                   
//                    //.Include(e => e.Error)
//                    .ApplayFilterCondition(
//                        _timer_id_RangeCondition,
//                        ids.ApplayProjectionCondition(_processIdDto_Id_Condition).ToArray())
//                    .ToArrayAsync(cancellationToken);
//            }

//            var containers = data.Select(
//                e =>
//                {
//                    {
//                        e.Error = new ProcessErrorDbEntity<TId>()
//                        {
//                            Id = e.Id,
//                            Error = null
//                        };
//                        var entry = _dbContext.Attach(e);
//                        entry.State = EntityState.Unchanged;
//                    }

//                    var container = new ProcessContainer<TId>(
//                        new EFProcessProxyComponent<TId>(e),
//                        new CurrentSessionComponent()
//                        {
//                            CreateRetryTimer = null,
//                            HaveError = false,
//                            IsSessionFirstStep = true,
//                            ReTryLimit = 3,
//                            RetryTimerCreated = false,
//                            SessionId = Guid.Empty
//                        });
//                    container.AddComponent(e);
//                    container.AddComponent<ITimerProcessComponent<TId>>(
//                        new TimerProcessComponent<TId>() 
//                        {
//                            LinkedProcess = null,
//                            LinkedProcessId = e.LinkedProcessId,
//                            TimerDate = e.TimerDate
//                        }
//                        );

//                    return container;
//                })
//                .ToDictionary(e => e.Process.Info.Id.Id, e => (IProcessContainer<TId>)e);

//            foreach (var elem in _processLoaders)
//            {
//                await elem.LoadForAsyncProcessingAsync(containers, cancellationToken);
//            }

//            return containers.Values;
//        }

//        public virtual async Task<ICollection<IProcessContainer<TId>>> GetRangeForAsyncProcessingAsync(
//            ICollection<ProcessIdDto<TId>> ids,
//            CancellationToken cancellationToken)
//        {
//            TTimerProcessEntity[] data;
//            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
//            {
//                var now = DateTimeOffset.UtcNow;

//                data = await _dbContext.Set<TTimerProcessEntity>()                                    
//                    //.Include(e => e.Error)
//                    .ApplayFilterCondition(_timer_id_RangeCondition, ids.Select(e => e.Id).ToArray())
//                    // Используем отдельный индекс.
//                    .ApplayFilterCondition(_process_AsyncExecute_Condition, now)
//                    .ToArrayAsync(cancellationToken);
//            }            

//            var containers = data.Select(
//                e =>
//                {
//                    {
//                        e.Error = new ProcessErrorDbEntity<TId>()
//                        {
//                            Id = e.Id,
//                            Error = null
//                        };
//                        var entry = _dbContext.Attach(e);
//                        entry.State = EntityState.Unchanged;
//                    }

//                    var container = new ProcessContainer<TId>(
//                        new EFProcessProxyComponent<TId>(e),
//                        new CurrentSessionComponent()
//                        {
//                            CreateRetryTimer = null,
//                            HaveError = false,
//                            IsSessionFirstStep = true,
//                            ReTryLimit = 3,
//                            RetryTimerCreated = false,
//                            SessionId = Guid.Empty
//                        });
//                    container.AddComponent<ITimerProcessComponent<TId>>(
//                        new TimerProcessComponent<TId>()
//                        {
//                            LinkedProcess = null,
//                            LinkedProcessId = e.LinkedProcessId,
//                            IsProcessOrTimer = e.IsProcessOrTimer,
//                            TimerDate = e.TimerDate
//                        }
//                        );
//                    return container;
//                })
//                .ToDictionary(e => e.Process.Info.Id.Id, e => (IProcessContainer<TId>)e);

//            foreach (var elem in _processLoaders)
//            {
//                await elem.LoadForAsyncProcessingAsync(containers, cancellationToken);
//            }

//            return containers.Values;
//        }

//        public virtual async Task UpdateAsync(
//            ICollection<IProcessContainer<TId>> processes,
//            CancellationToken cancellationToken)
//        {
//            foreach (var elem in processes)
//            {
//                if (
//                    elem.CurrentSession.CreateRetryTimer.HasValue
//                    && !elem.CurrentSession.RetryTimerCreated)
//                {
//                    // TODO: create retry timers
//                }
//            }

//            foreach (var elem in _processLoaders)
//            {
//                await elem.UpdateAsync(processes, cancellationToken);
//            }

//            await _dbContext.SaveChangesAsync(cancellationToken);
//        }
//    }
//}
