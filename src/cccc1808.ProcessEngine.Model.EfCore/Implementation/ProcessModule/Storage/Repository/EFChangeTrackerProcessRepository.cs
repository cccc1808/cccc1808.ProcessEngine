using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository
{
    public class EFChangeTrackerProcessRepository<TId, TDbEntity>
        : IProcessRepository<TId>
        where TDbEntity : ProcessDbEntity<TId>
    {
        protected readonly IEFDbContext _dbContext;
        protected readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IProcessRegistry _processRegistry;
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IEnumerable<IProcessDbProvider<TId>> _processLoaders;        

        private readonly IProcessDbEntityConditions<TId, TDbEntity> _processDbEntityConditions;
        private readonly IProcessErrorDbEntityConditions<TId> _processErrorDbEntityConditions;

        public EFChangeTrackerProcessRepository(
            IEFDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IIdGenerator<TId> idGenerator,
            IProcessRegistry processRegistry,
            IEnumerable<IProcessDbProvider<TId>> processLoaders,

            IProcessDbEntityConditions<TId, TDbEntity> processDbEntityConditions,
            IProcessErrorDbEntityConditions<TId> processErrorDbEntityConditions)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _processRegistry = processRegistry;
            _idGenerator = idGenerator;
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
                            CurrentSessionHaveError = false,
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

        public virtual async Task<ICollection<IProcessContainer<TId>>> GetForAsyncProcessingRangeAsync(
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
                                    retryLimit: 2,
                                    haveErrorOnStart: e.StoppedByError || e.RetryCount.HasValue)
                                {
                                    CurrentSessionHaveError = false,
                                    IsSessionFirstStep = true,
                                    RetryLimit = 2,
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

        public async Task<ICollection<IProcessContainer<TId>>> GetWaitingRangeAsync(
            ICollection<TId> ids,
            bool updateLock, 
            CancellationToken cancellationToken)
        {
            Dictionary<TId, IProcessContainer<TId>> containers;
            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
            {
                var data = await _dbContext.Set<TDbEntity>()
                    .ApplayQueryCondition(
                    _processDbEntityConditions.WaitEvent.QueryIds,
                        ids)
                    .ToArrayAsync(cancellationToken);

                containers = data
                    .Select(
                        e =>
                        {
                            return (IProcessContainer<TId>)new ProcessContainer<TId>(
                                new EFProcessProxyComponent<TId>(e),
                                new AsyncSessionComponent(
                                    retryLimit: 3,
                                    haveErrorOnStart: e.StoppedByError || e.RetryCount.HasValue)
                                {
                                    CurrentSessionHaveError = false,
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
                await elem.LoadRangeAsync(
                    containers,
                    byTypeIndex,
                    withLock: false,
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

            // 1) Вызываем логику хендлеров для сохранения дополнительного состояния.
            foreach (var elem in _processLoaders)
            {
                await elem.UpdateAsync(
                    processes, 
                    byTypeIndex,
                    cancellationToken);
            }

            // 2) Реализация, чтобы загружать данные об ошибке, только по необходимости, а не на каждый запрос.            
            {
                var errorSet = _dbContext.Set<ProcessErrorDbEntity<TId>>();

                var errorStateChanged = processes
                    .Where(e => e.CurrentSession.NeedUpdateErrorData)
                    .ToArray();
                var errorEntries = new List<EntityEntry<ProcessErrorDbEntity<TId>>>(errorStateChanged.Length);

                try
                {
                    if (errorStateChanged.Any())
                    {
                        var errorDbEntities = await errorSet
                            .ApplayQueryCondition(
                                _processErrorDbEntityConditions.ProcessLinkedDbEntity.QueryRange,
                                errorStateChanged.Select(e => e.Id).ToArray())
                            .Select(e => new { e.ProcessId, e.Id }) // Без проекции создает Entity и подсоединяет ее в ChangeTracker (можно AsNoTracking, но создание сущности все равно не нужно).
                            .ToDictionaryAsync(e => e.ProcessId, e => e.Id, cancellationToken);

                        foreach (var elem in errorStateChanged)
                        {
                            if (errorDbEntities.TryGetValue(elem.Id, out var errorEntityId))
                            {
                                var updateEntity = new ProcessErrorDbEntity<TId>(
                                    errorEntityId,
                                    elem.Process.Info.Id,
                                    elem.Process.Error?.ErrorJson,
                                    elem.Process.Error?.Date,
                                    elem.Process.Error?.SessionId);

                                var entry = errorSet.Attach(updateEntity);
                                entry.State = EntityState.Modified;
                                errorEntries.Add(entry);
                            }
                            else
                            {
                                var createEntity = new ProcessErrorDbEntity<TId>(
                                    await _idGenerator.NextAsync(cancellationToken),
                                    elem.Process.Info.Id,
                                    elem.Process.Error?.ErrorJson,
                                    elem.Process.Error?.Date,
                                    elem.Process.Error?.SessionId);

                                var entry = errorSet.Attach(createEntity);
                                entry.State = EntityState.Added;
                                errorEntries.Add(entry);
                            }
                        }
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    foreach (var elem in errorEntries)
                    {
                        elem.State = EntityState.Detached;
                    }
                }

                foreach (var elem in errorStateChanged)
                {
                    elem.CurrentSession.NeedUpdateErrorData = false;
                }
            }
        }

        public async Task UpdateWakeupAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            // [Hack]: Немного костыль, но вот так (чтобы запись не обновлялась при промежуточных сохранениях, не ставился lock):
            foreach (var elem in processes)
            {
                if (elem.TryGetComponent<IWakeUpComponent>(out var component))
                {
                    if (component is not EFWakeUpProxyComponent<TId> proxy)
                    {
                        throw new Exception("[Bug]");
                    }

                    var entry = _dbContext.DbContext.Set<ProcessWakeUpDbEntity<TId>>().Attach(proxy.DbEntity);
                    entry.State = component.NeedUpdate 
                        ? EntityState.Modified
                        : EntityState.Unchanged;
                }
            }

            // Код вызывается после финального сохрания
            // (иначе нельзя было бы гарантировать актуальность проверки IWakeupCheckHandler).
            // Поэтому сохраняем еще раз.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
