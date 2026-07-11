using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Dtos;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Entities;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Services;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Implementation.Services
{
    public class EFStaticInstanceDeployService<TId> : IStaticInstanceDeployService
    {
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IEFDbContext _dbContext;
        private readonly IProcessRegistry _processRegistry;
        private readonly IStaticInstanceRegistry _registry;
        private readonly IStaticInstanceHandler<TId> _staticInstanceHandler;

        public EFStaticInstanceDeployService(
            ILockQueryHintStore lockQueryHintStore, 
            IIdGenerator<TId> idGenerator,
            IEFDbContext dbContext,
            IProcessRegistry processRegistry,
            IStaticInstanceRegistry registry, 
            IStaticInstanceHandler<TId> staticInstanceHandler)
        {
            _lockQueryHintStore = lockQueryHintStore;
            _idGenerator = idGenerator;
            _dbContext = dbContext;
            _processRegistry = processRegistry;
            _registry = registry;
            _staticInstanceHandler = staticInstanceHandler;
        }

        public async Task<bool> TryExecuteAsync(CancellationToken cancellationToken)
        {
            var deployVersion = _registry.GetDeployVersion();

            var all = _registry.All();

            // 1) Блокируем deploy.
            StaticInstanceDeployDbEntity<TId> deployInfo;
            {
                var insertResult = await _dbContext
                    .Set<StaticInstanceDeployDbEntity<TId>>()
                    .Upsert(new StaticInstanceDeployDbEntity<TId>(await _idGenerator.NextAsync(cancellationToken), deployVersion))
                    .On(e => e.Id)
                    .NoUpdate()
                    .RunAndReturnAsync(cancellationToken);
                deployInfo = insertResult.FirstOrDefault();

                if (deployInfo is null)
                {
                    using (_ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                    {
                        deployInfo = await _dbContext
                            .Set<StaticInstanceDeployDbEntity<TId>>()
                            .FirstAsync(cancellationToken);
                    }

                    if (deployInfo is null)
                    {
                        // Обрабатывается другой нодой, ждем блокировку.
                        return false;
                    }

                    if (deployInfo.Version >= deployVersion)
                    {
                        // Уже был деплой более новой версии, ничего не обновляем.
                        return true;
                    }
                }
            }

            // 2) Собираем данные.
            var needCreate = new HashSet<StaticInstanceProcessRegistrationDto>(all.Count);
            var needRemove = new Dictionary<TId, StaticInstanceProcessRegistrationDto>(all.Count);
            {
                var exsistRegistrations = await _dbContext
                    .Set<StaticInstanceRegistrationDbEntity<TId>>()
                    .ToArrayAsync(cancellationToken);

                var exsistRegistrationSet = exsistRegistrations.ToDictionary(
                    e => new StaticInstanceProcessRegistrationDto(e.ProcessType, e.InstanceKey), 
                    e => e);

                foreach (var elem in all)
                {
                    if (!exsistRegistrationSet.TryGetValue(elem, out _))
                    {
                        needCreate.Add(elem);
                        exsistRegistrationSet.Remove(elem);
                    }
                }    

                foreach (var elem in exsistRegistrationSet)
                {
                    needRemove.Add(elem.Value.ProcessId, elem.Key);
                    exsistRegistrationSet.Remove(elem.Key);
                }
            }

            // 3) Обновляем
            {
                // Удаляем.
                if (needRemove.Any())
                {
                    Dictionary<StaticInstanceProcessRegistrationDto, TId> lockedProcesses;
                    using (_ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                    {
                        var data = await _dbContext.Set<ProcessDbEntity<TId>>()
                            .Where(e => needRemove.Keys.Contains(e.Id))
                            .Select(e => e.Id)
                            .ToArrayAsync(cancellationToken);

                        lockedProcesses = data.ToDictionary(e => needRemove[e], e => e);
                    }

                    if (lockedProcesses.Any())
                    {
                        await _staticInstanceHandler.RemoveProcessRangeAsync(
                            lockedProcesses,
                            cancellationToken);

                        await _dbContext
                            .Set<StaticInstanceRegistrationDbEntity<TId>>()
                            .Where(e => lockedProcesses.Values.Contains(e.ProcessId))
                            .ExecuteDeleteAsync(cancellationToken);
                    }

                    // Не смогли получить блокировку над всеми процессами для удаления.
                    if (needRemove.Count != lockedProcesses.Count)
                    {
                        return false;
                    }
                }

                // Создаем.
                if (needCreate.Any())
                {
                    var processIds = await _staticInstanceHandler.CreateProcessRangeAsync(
                        needCreate,
                        cancellationToken);

                    var ids = await _idGenerator.NextRangeAsync(processIds.Count, cancellationToken);

                    _dbContext
                        .Set<StaticInstanceRegistrationDbEntity<TId>>()
                        .AddRange(
                            processIds.Select(
                                e => new StaticInstanceRegistrationDbEntity<TId>(
                                    ids.Dequeue(),
                                    e.Key.ProcessType,
                                    e.Key.Key,
                                    e.Value)
                                )
                        );
                }

                // Обновляем версию.
                deployInfo.Version = deployVersion;
                return true;
            }
        }

        public void Validate()
        {
            var all = _registry.All();
            var processes = _processRegistry.All().Select(e => e.ProcessType.ProcessType).ToHashSet();

            foreach (var elem in all)
            {
                if (!_staticInstanceHandler.CanProcess(elem))
                {
                    throw new InvalidOperationException($"{elem}");
                }

                if (!processes.Contains(elem.ProcessType))
                {
                    throw new InvalidOperationException($"{elem}");
                }
            }
        }
    }
}
