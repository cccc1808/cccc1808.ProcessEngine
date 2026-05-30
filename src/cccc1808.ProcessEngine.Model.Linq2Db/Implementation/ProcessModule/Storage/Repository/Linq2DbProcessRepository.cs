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
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using LinqToDB.DataProvider.PostgreSQL;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository
{
    public class Linq2DbProcessRepository<TId, TDbEntity>
        : IProcessRepository<TId>
        where TDbEntity : ProcessDbEntity<TId>
    {
        protected readonly ILinq2DbDataConnection _dataConnection;
        protected readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IEnumerable<IProcessDbProvider<TId>> _processLoaders;
        private readonly IWakeupRegistry<TId> _wakeupRegistry;
        private readonly Options _options;

        private readonly IProcessDbEntityConditions<TId, TDbEntity> _processDbEntityConditions;
        private readonly IProcessErrorDbEntityConditions<TId> _processErrorDbEntityConditions;

        public Linq2DbProcessRepository(
            ILinq2DbDataConnection dataConnection,
            ILockQueryHintStore lockQueryHintStore,
            IDateTimeProvider dateTimeProvider,
            IIdGenerator<TId> idGenerator,
            IEnumerable<IProcessDbProvider<TId>> processLoaders,
            IWakeupRegistry<TId> wakeupRegistry,
            Options options,

            IProcessDbEntityConditions<TId, TDbEntity> processDbEntityConditions,
            IProcessErrorDbEntityConditions<TId> processErrorDbEntityConditions)
        {
            _dataConnection = dataConnection;
            _lockQueryHintStore = lockQueryHintStore;
            _dateTimeProvider = dateTimeProvider;
            _idGenerator = idGenerator;
            _processLoaders = processLoaders;
            _wakeupRegistry = wakeupRegistry;
            _options = options;

            _processDbEntityConditions = processDbEntityConditions;
            _processErrorDbEntityConditions = processErrorDbEntityConditions;
        }

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
                {
                    var data = await _dataConnection.Set<TDbEntity>()
                        .QueryHint(PostgresQueryHint.ForNoKeyUpdateSkipLocked)
                        .Where(e => notLoadedProcesses.Values.Select(e => e.Id).Contains(e.Id))
                        .ToArrayAsync(cancellationToken);

                    foreach (var elem in data)
                    {
                        // Так как мы уже считали с блокировкой,
                        // то в конце текущей транзакции тожно сбросить SelectLock, т.к. сессия работы была завершена.
                        // Не сбрасываем на min, потому что значение используется.
                        elem.SelectLockTimeout = _dateTimeProvider.UtcNow;

                        var container = MapContainer(
                                _options,
                                _wakeupRegistry,
                                elem,
                                isAsyncExecuting: true);

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
                var data = await _dataConnection.Set<TDbEntity>()
                    .ApplayQueryCondition(
                        _processDbEntityConditions.WaitEvent.QueryIds,
                        ids)
                    .ToArrayAsync(cancellationToken);

                containers = data
                    .Select(
                        e =>
                        {
                            return (IProcessContainer<TId>)MapContainer(
                                _options,
                                _wakeupRegistry,
                                e,
                                isAsyncExecuting: false);
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
                var errorSet = _dataConnection.Set<ProcessErrorDbEntity<TId>>();

                var errorStateChanged = processes
                    .Where(e => e.CurrentSession.NeedUpdateErrorData)
                    .ToArray();

                if (errorStateChanged.Any())
                {
                    var errorDbEntities = await errorSet
                        .ApplayQueryCondition(
                            _processErrorDbEntityConditions.ProcessLinkedDbEntity.QueryRange,
                            errorStateChanged.Select(e => e.Id).ToArray())
                        .ToDictionaryAsync(e => e.ProcessId, e => e.Id, cancellationToken);

                    var create = new List<ProcessErrorDbEntity<TId>>(errorStateChanged.Length);
                    var update = new List<ProcessErrorDbEntity<TId>>(errorStateChanged.Length);
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
                            update.Add(updateEntity);
                        }
                        else
                        {
                            var createEntity = new ProcessErrorDbEntity<TId>(
                                await _idGenerator.NextAsync(cancellationToken),
                                elem.Process.Info.Id,
                                elem.Process.Error?.ErrorJson,
                                elem.Process.Error?.Date,
                                elem.Process.Error?.SessionId);
                            create.Add(createEntity);
                        }
                    }

                    if (create.Any())
                    {
                        await _dataConnection.DataConnection.BulkCopyAsync(create, cancellationToken);
                    }
                    if (update.Any()) 
                    {
                        await errorSet.Merge()
                            .Using(update)
                            .OnTargetKey()
                            .UpdateWhenMatched()
                            .MergeAsync(cancellationToken);
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

            // 3) Сохраняем процесс.
            {
                var forUpdate = processes
                    .Select(e => new ProcessDbEntity<TId>(
                        e.Id,
                        e.Process.Info.ProcessType.ProcessType,
                        e.Process.Info.ProcessType.ProcessVersion,
                        e.Process.Info.Priority,
                        e.Process.SelectLockTimeout,
                        e.Process.StoppedByError,
                        e.Process.Status,
                        e.Process.RetryCount))
                    .ToArray();

                await _dataConnection.Set<ProcessDbEntity<TId>>()
                    .Merge()
                    .Using(forUpdate)
                    .OnTargetKey()
                    .UpdateWhenMatched()
                    .MergeAsync(cancellationToken);
            }
        }

        public async Task UpdateWakeupAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var executeProcess = new List<TId>(processes.Count);
            var executeWakeup = new List<TId>(processes.Count);
            var noExecuteProcess = new List<TId>(processes.Count);
            var noExecuteWakeup = new List<TId>(processes.Count);

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

                if (component.IsAsyncExecuting)
                {
                    executeProcess.Add(elem.Process.Info.Id);
                    if (component.HaveWakeupEntity)
                    {
                        executeWakeup.Add(elem.Process.Info.Id);
                    }
                }
                else
                {
                    noExecuteProcess.Add(elem.Process.Info.Id);
                    if (component.HaveWakeupEntity)
                    {
                        noExecuteWakeup.Add(elem.Process.Info.Id);
                    }
                }
            }

            if (executeProcess.Any())
            {
                await _dataConnection.Set<TDbEntity>()
                    .Where(e => executeProcess.Contains(e.Id))
                    .Set(e => e.Status, ProcessStatusEnum.AsyncExecute)
                    .UpdateAsync(cancellationToken);
            }
            if (executeWakeup.Any())
            {
                await _dataConnection.Set<ProcessWakeupDbEntity<TId>>()
                    .Where(e => executeWakeup.Contains(e.ProcessId))
                    .Set(e => e.IsAsyncExecuting, true)
                    .UpdateAsync(cancellationToken);
            }

            if (noExecuteProcess.Any())
            {
                await _dataConnection.Set<TDbEntity>()
                    .Where(e => noExecuteProcess.Contains(e.Id))
                    .Set(e => e.Status, ProcessStatusEnum.WaitEvent)
                    .UpdateAsync(cancellationToken);
            }
            if (noExecuteWakeup.Any())
            {
                await _dataConnection.Set<ProcessWakeupDbEntity<TId>>()
                    .Where(e => noExecuteWakeup.Contains(e.ProcessId))
                    .Set(e => e.IsAsyncExecuting, false)
                    .UpdateAsync(cancellationToken);
            }
        }

        private static ProcessContainer<TId> MapContainer(
            Options options,
            IWakeupRegistry<TId> wakeupRegistry,
            TDbEntity source,
            bool isAsyncExecuting)
        {
            return new ProcessContainer<TId>(
                MapComponent(source),
                new AsyncSessionComponent(
                    sessionId: Guid.Empty,
                    isSessionFirstStep: true,
                    currentSessionHaveError: false,
                    retryLimit: options.RetryLimit,
                    stopAsyncProcessingSession: false,
                    needUpdateErrorData: false,
                    haveErrorOnStart: source.StoppedByError || source.RetryCount.HasValue, // TODO: condition
                    clearErrorOnSessionEnd: true
                    ),
                isAsyncExecuting: isAsyncExecuting,
                wakeupState: wakeupRegistry.CheckWakeup(new ProcessTypeDto(source.ProcessTypeId, source.ProcessVersion))
                );
        }

        private static ProcessComponent<TId> MapComponent(TDbEntity source)
        {
            return new ProcessComponent<TId>(
                new ProcessInstanceInfoDto<TId>(
                    source.Id, 
                    new ProcessTypeDto(source.ProcessTypeId, source.ProcessVersion), 
                    source.Priority
                    ),
                source.StoppedByError,
                source.Status,
                source.RetryCount,
                null,
                source.SelectLockTimeout
                );
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
