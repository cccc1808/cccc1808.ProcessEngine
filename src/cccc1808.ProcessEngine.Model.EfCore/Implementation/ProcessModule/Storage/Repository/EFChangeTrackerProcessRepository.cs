using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository
{
    public class EFChangeTrackerProcessRepository<TId, TDbEntity>
        : IProcessRepository<TId>
        where TDbEntity : ProcessDbEntity<TId>
    {
        protected readonly IEFDbContext _dbContext;
        protected readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IProcessRegistry _processRegistry;
        private readonly IEnumerable<IProcessDbProvider<TId>> _processLoaders;

        private readonly IProcessDbEntityConditions<TId, TDbEntity> _processDbEntityConditions;
        private readonly IProcessErrorDbEntityConditions<TId> _processErrorDbEntityConditions;

        public EFChangeTrackerProcessRepository(
            IEFDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IProcessRegistry processRegistry,
            IEnumerable<IProcessDbProvider<TId>> processLoaders,

            IProcessDbEntityConditions<TId, TDbEntity> processDbEntityConditions,
            IProcessErrorDbEntityConditions<TId> processErrorDbEntityConditions)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _processRegistry = processRegistry;
            _processLoaders = processLoaders;

            _processDbEntityConditions = processDbEntityConditions;
            _processErrorDbEntityConditions = processErrorDbEntityConditions;
        }

        public virtual async Task<ICollection<IProcessContainer<TId>>> GetRange(
            ICollection<TId> ids,
            bool withLock,
            CancellationToken cancellationToken)
        {
            TDbEntity[] data;
            using (var hint = _lockQueryHintStore.StartScope(withLock ? LockHintEnum.ForNoKeyUpdateAndSkipLocked : LockHintEnum.No))
            {
                data = await _dbContext.Set<TDbEntity>()
                    //.Include(e => e.Error)
                    .ApplayQueryCondition(
                        _processDbEntityConditions.Id.QueryRange,
                        ids.Select(e => e).ToArray())
                    .ToArrayAsync(cancellationToken);
            }

            var containers = data.Select(
                e =>
                {
                    return new ProcessContainer<TId>(
                        new EFProcessProxyComponent<TId>(e),
                        new AsyncSessionComponent(
                            retryLimit: 3, 
                            haveErrorOnStart: e.StoppedByError || e.RetryCount.HasValue)
                        {
                            HaveError = false,
                            IsSessionFirstStep = true,
                            SessionId = Guid.Empty,
                            StopAsyncProcessingSession = false,
                        });
                })
                .ToDictionary(e => e.Process.Info.Id, e => (IProcessContainer<TId>)e);

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
            ICollection<TId> ids,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            Dictionary<TId, IProcessContainer<TId>> containers;
            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var data = await _dbContext.Set<TDbEntity>()
                    .ApplayQueryCondition(
                    _processDbEntityConditions.DbProcessingForHandler.Query,
                    new IProcessDbEntityConditions<TId, TDbEntity>.DbProcessingForSelectorHandlerParameters(
                        now,
                        _dbContext,
                        _processRegistry.All(),
                        ids))
                    //.Include(e => e.Error)
                    .ToArrayAsync(cancellationToken);

                containers = data
                    .Select(
                        e =>
                        {
                            // Так как мы уже считали с блокировкой,
                            // то в конце текущей транзакции тожно сбросить SelectLock, т.к. сессия работы была завершена.
                            // Не сбрасываем на min, потому что значение используется.
                            e.SelectLockTimeout = DateTimeOffset.UtcNow;

                            return (IProcessContainer<TId>)new ProcessContainer<TId>(
                                new EFProcessProxyComponent<TId>(e),
                                new AsyncSessionComponent(
                                    retryLimit: 3,
                                    haveErrorOnStart: e.StoppedByError || e.RetryCount.HasValue)
                                {
                                    HaveError = false,
                                    IsSessionFirstStep = true,
                                    RetryLimit = 3,
                                    SessionId = Guid.Empty
                                });
                        }
                        )
                    .ToDictionary(e => e.Id, e => e);
            }

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

            IProcessContainer<TId>[] errorStateChanged;
            {
                // Реализация, чтобы не загружать блоки ошибок.
                errorStateChanged = processes
                    .Where(e => e.CurrentSession.NeedUpdateErrorData)
                    .ToArray();

                var errorDbEntities = await _dbContext.Set<ProcessErrorDbEntity<TId>>()
                    .ApplayQueryCondition(
                        _processErrorDbEntityConditions.ProcessLinkedDbEntity.QueryRange,
                        errorStateChanged.Select(e => e.Id).ToArray())
                    .ToDictionaryAsync(e => e.ProcessId, e => e.Id, cancellationToken);

                foreach (var elem in errorStateChanged)
                {
                    var errorEntityId = errorDbEntities[elem.Id];

                    var updateEntity = new ProcessErrorDbEntity<TId>() 
                    {
                        Id = errorEntityId,
                        Error = elem.Process.Error?.ErrorJson,
                        ErrorDate = elem.Process.Error?.Date,
                        ErrorSessionId = elem.Process.Error?.SessionId,
                        ProcessId = elem.Process.Info.Id,
                    };

                    _dbContext.Set<ProcessErrorDbEntity<TId>>()
                        .Attach(updateEntity)
                        .State = EntityState.Modified;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var elem in errorStateChanged)
            {
                elem.CurrentSession.NeedUpdateErrorData = false;
            }
        }

        public async Task UpdateWakeupAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
