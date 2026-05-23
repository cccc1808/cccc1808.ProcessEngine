using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;

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
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IIdGenerator<TId> _idGenerator;        
        private readonly IEnumerable<IProcessDbProvider<TId>> _processLoaders;
        private readonly IWakeupRegistry<TId> _wakeupRegistry;
        private readonly Options _options;

        private readonly IProcessDbEntityConditions<TId, TDbEntity> _processDbEntityConditions;
        private readonly IProcessErrorDbEntityConditions<TId> _processErrorDbEntityConditions;        

        public EFChangeTrackerProcessRepository(
            IEFDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IDateTimeProvider dateTimeProvider,
            IIdGenerator<TId> idGenerator,
            IEnumerable<IProcessDbProvider<TId>> processLoaders,
            IWakeupRegistry<TId> wakeupRegistry,
            Options options,

            IProcessDbEntityConditions<TId, TDbEntity> processDbEntityConditions,
            IProcessErrorDbEntityConditions<TId> processErrorDbEntityConditions)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _dateTimeProvider = dateTimeProvider;
            _idGenerator = idGenerator;
            _processLoaders = processLoaders;
            _wakeupRegistry = wakeupRegistry;
            _options = options;

            _processDbEntityConditions = processDbEntityConditions;
            _processErrorDbEntityConditions = processErrorDbEntityConditions;
        }

        //public virtual async Task<ICollection<IProcessContainer<TId>>> GetRange(
        //    ICollection<TId> ids,
        //    bool withLock,
        //    CancellationToken cancellationToken)
        //{
        //    TDbEntity[] data;
        //    using (var hint = _lockQueryHintStore.StartScope(withLock ? LockHintEnum.ForNoKeyUpdateAndSkipLocked : LockHintEnum.No))
        //    {
        //        data = await _dbContext.Set<TDbEntity>()
        //            //.Include(e => e.Error)
        //            .ApplayQueryCondition(
        //                _processDbEntityConditions.Id.QueryRange,
        //                ids.Select(e => e).ToArray())
        //            .ToArrayAsync(cancellationToken);
        //    }

        //    var containers = data.Select(
        //        e =>
        //        {
        //            return ProcessContainer < TId >.
        //            new ProcessContainer<TId>(
        //                new EFProcessProxyComponent<TId>(e),
        //                new AsyncSessionComponent(
        //                    sessionId: Guid.Empty,
        //                    isSessionFirstStep: true,
        //                    currentSessionHaveError: false,
        //                    retryLimit: RetryParameter,
        //                    stopAsyncProcessingSession: false,
        //                    needUpdateErrorData: false,
        //                    haveErrorOnStart: e.StoppedByError || e.RetryCount.HasValue // TODO: condition                            
        //                    ));
        //        })
        //        .ToDictionary(e => e.Process.Info.Id, e => (IProcessContainer<TId>)e);

        //    var byTypeIndex = containers.Values
        //        .GroupBy(e => e.Process.Info.ProcessType)
        //        .ToDictionary(
        //            e => e.Key, 
        //            e => (ICollection<TId>)e.Select(e => e.Id).ToArray());
        //    foreach (var elem in _processLoaders)
        //    {
        //        await elem.LoadRangeAsync(
        //            containers,
        //            byTypeIndex,
        //            withLock,
        //            cancellationToken);
        //    }

        //    return containers.Values;
        //}

        public virtual async Task<ICollection<IProcessContainer<TId>>> GetForAsyncProcessingRangeAsync(
            ICollection<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancellationToken)
        {
            var byTypeIndex = ids
                .GroupBy(e => e.ProcessType)
                .ToDictionary(
                    e => e.Key,
                    e => (ICollection<TId>)e.Select(e => e.Id).ToArray());
            var loadBuffer = new Dictionary<TId, IProcessContainer<TId>>(ids.Count);
            var notLoadedProcesses = ids.ToDictionary(e => e.Id, e => e);

            var softTimeout = _options.SoftTimeout.HasValue
                ? DateTimeOffset.Now + _options.SoftTimeout.Value
                : (DateTimeOffset?)null;

            // Кастомные загрузчики процессов.
            // Используется процессами стримами (с сообщениями), чтобы загружать батчи сообщений и не блокировать процессы, в батч сообщения не поместились.
            foreach (var elem in _processLoaders)
            {
                await elem.LoadProcessForAsyncProcessingAsync(                    
                    notLoadedProcesses,
                    loadBuffer,
                    byTypeIndex,
                    cancellationToken);
            }

            // Логика загрузки по умолчанию.
            if (notLoadedProcesses.Any())
            {
                using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                {
                    var data = await _dbContext.Set<TDbEntity>()
                        .Where(e => notLoadedProcesses.Values.Select(e => e.Id).Contains(e.Id))
                        .ToArrayAsync();

                    // В2 Коррелированный подзапрос
                    //var data = await _dbContext.Set<TDbEntity>()
                    //    .ApplayQueryCondition(
                    //        _processDbEntityConditions.DbProcessingForHandler.Query,
                    //        new IProcessDbEntityConditions<TId, TDbEntity>.DbProcessingForSelectorHandlerParameters(
                    //            _dbContext,
                    //            _processRegistry.All(),
                    //            notLoadedProcesses.Keys))
                    //    .ToArrayAsync(cancellationToken);

                    foreach (var elem in data)
                    {
                        // Так как мы уже считали с блокировкой,
                        // то в конце текущей транзакции тожно сбросить SelectLock, т.к. сессия работы была завершена.
                        // Не сбрасываем на min, потому что значение используется.
                        elem.SelectLockTimeout = _dateTimeProvider.UtcNow;

                        var container = new ProcessContainer<TId>(
                            new EFProcessProxyComponent<TId>(elem),
                            new AsyncSessionComponent(
                                sessionId: Guid.Empty,
                                isSessionFirstStep: true,
                                currentSessionHaveError: false,
                                retryLimit: _options.RetryLimit,
                                stopAsyncProcessingSession: false,
                                needUpdateErrorData: false,
                                haveErrorOnStart: elem.StoppedByError || elem.RetryCount.HasValue, // TODO: condition                                                                                               
                                clearErrorOnSessionEnd: true
                                ),
                            isAsyncExecuting: true,
                            wakeupState: _wakeupRegistry.CheckWakeup(new ProcessTypeDto(elem.ProcessTypeId, elem.ProcessVersion))
                            );

                        if (softTimeout.HasValue)
                        {
                            container.AddComponent<ISoftTimeoutComponent>(
                                new SoftTimeoutComponent(softTimeout));
                        }

                        loadBuffer.Add(elem.Id, container);
                    }
                    notLoadedProcesses.Clear();
                }
            }
            
            // Загрузка связанных данных процесса.
            foreach (var elem in _processLoaders)
            {
                await elem.LoadProcessDataAsync(
                    loadBuffer,
                    byTypeIndex,
                    isAsyncExecution: true,
                    cancellationToken);
            }

            return loadBuffer.Values;
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
                                    sessionId: Guid.Empty,
                                    isSessionFirstStep: true,
                                    currentSessionHaveError: false,
                                    retryLimit: _options.RetryLimit,
                                    stopAsyncProcessingSession: false,
                                    needUpdateErrorData: false,
                                    haveErrorOnStart: e.StoppedByError || e.RetryCount.HasValue, // TODO: condition                                                                                                   
                                    clearErrorOnSessionEnd: true
                                    ),
                                isAsyncExecuting: false,
                                wakeupState: _wakeupRegistry.CheckWakeup(new ProcessTypeDto(e.ProcessTypeId, e.ProcessVersion))                                
                                );
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
                await elem.LoadProcessDataAsync(
                    containers,
                    byTypeIndex,
                    isAsyncExecution: false,
                    cancellationToken);
            }

            return containers.Values;
        }

        public virtual async Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var processesDictionary = processes
                .ToDictionary(e => e.Id, e => e);

            var byTypeIndex = processes
                .GroupBy(e => e.Process.Info.ProcessType)
                .ToDictionary(
                    e => e.Key,
                    e => (ICollection<TId>)e.Select(e => e.Id).ToArray());

            // 1) Вызываем логику хендлеров для сохранения дополнительного состояния.
            foreach (var elem in _processLoaders)
            {
                await elem.UpdateAsync(
                    processesDictionary, 
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

                                var entry = _dbContext.AttachEntity(
                                    updateEntity, 
                                    throwIfAttached: true);
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

                                var entry = _dbContext.AttachEntity(
                                    createEntity,
                                    throwIfAttached: true);
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
                        _dbContext.Detach(elem);
                    }
                }

                foreach (var elem in errorStateChanged)
                {
                    if (elem.CurrentSession != null)
                    {
                        elem.CurrentSession.NeedUpdateErrorData = false;
                    }                    
                }
            }
        }

        public async Task UpdateWakeupAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            foreach (var elem in processes)
            {
                var component = elem.GetComponent<IWakeupComponent<TId>>();

                if (!component.HaveWakeupEntity)
                {
                    continue;
                }

                if (!component.NeedUpdate)
                {
                    continue;
                }

                var entry = _dbContext.AttachEntity(
                    new ProcessWakeupDbEntity<TId>(
                        id: component.Id,
                        processId: elem.Id,
                        isAsyncExecuting: component.IsAsyncExecuting
                        ),
                    throwIfAttached: true);
                entry.State = EntityState.Modified;
            }
            
            // Код вызывается после финального сохрания
            // (иначе нельзя было бы гарантировать актуальность проверки IWakeupCheckHandler).
            // Поэтому сохраняем еще раз.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }


        public class Options
        {
            public short RetryLimit { get; set; } 
                = 2;

            public TimeSpan? SoftTimeout { get; set; }
                = TimeSpan.FromMinutes(1);
        }
    }
}
