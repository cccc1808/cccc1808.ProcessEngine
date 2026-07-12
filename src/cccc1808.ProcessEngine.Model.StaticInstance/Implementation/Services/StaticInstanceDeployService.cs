using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Dtos;
using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Services;


namespace cccc1808.ProcessEngine.Model.StaticInstance.Implementation.Services
{
    public class StaticInstanceDeployService<TId> : IStaticInstanceDeployService
    {
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IQueries _queries;
        private readonly IProcessRegistry _processRegistry;
        private readonly IStaticInstanceRegistry _registry;
        private readonly IStaticInstanceHandler<TId> _staticInstanceHandler;

        public StaticInstanceDeployService(
            ILockQueryHintStore lockQueryHintStore, 
            IIdGenerator<TId> idGenerator,
            IQueries queries,
            IProcessRegistry processRegistry,
            IStaticInstanceRegistry registry, 
            IStaticInstanceHandler<TId> staticInstanceHandler)
        {
            _lockQueryHintStore = lockQueryHintStore;
            _idGenerator = idGenerator;
            _queries = queries;
            _processRegistry = processRegistry;
            _registry = registry;
            _staticInstanceHandler = staticInstanceHandler;
        }

        #region IStaticInstanceDeployService

        public async Task<bool> TryExecuteAsync(CancellationToken cancellationToken)
        {
            var all = _registry.All();

            var context = _queries.PrepareContext(
                _registry.GetDeployVersion());

            // 1) Создаем или получаем существующий deploy с блокировкой.
            {
                await _queries.CreateOrTryGetDeployWithLockAsync(context, cancellationToken);

                if (context.DbDeploy is null)
                {
                    // Обрабатывается другой нодой, ждем блокировку.
                    return false;
                }

                if (!context.DeployCreated && context.DbDeploy.Value.Version >= context.DeployVersion)
                {
                    // Уже был деплой более новой версии, ничего не обновляем.
                    return true;
                }
            }

            // 2) Собираем данные.
            var needCreate = new HashSet<StaticInstanceProcessRegistrationDto>(all.Count);
            var needRemove = new Dictionary<TId, StaticInstanceProcessRegistrationDto>(all.Count);
            {
                var dbRegistrations = await _queries.LoadRegistrationsAsync(context, cancellationToken);
                
                var exsistRegistrationSet = dbRegistrations
                    .ToDictionary(e => e.StaticInstanceRegistration, e => e);

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
                    var lockedProcessesIds = await _queries.TryLockProcessesAsync(
                        context,
                        needRemove.Keys, 
                        cancellationToken);
                    var lockedProcesses = lockedProcessesIds.ToDictionary(e => needRemove[e], e => e);

                    if (lockedProcessesIds.Any())
                    {
                        await _staticInstanceHandler.RemoveProcessRangeAsync(
                            lockedProcesses,
                            cancellationToken);

                        await _queries.RemoveRegistrationsAsync(
                            context, 
                            lockedProcesses.Keys,
                            cancellationToken);
                    }

                    // Не смогли получить блокировку над всеми процессами для удаления.
                    if (needRemove.Count != lockedProcessesIds.Count)
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

                    await _queries.CreateRegistrationsAsync(
                        context,
                        processIds.Select(e => new IQueries.RegistrationInfo(e.Key, e.Value)).ToArray(),
                        cancellationToken
                        );
                }

                await _queries.UpdateDeployVersionAsync(context, cancellationToken);
                return true;
            }
        }

        public void Validate()
        {
            var all = _registry.All();
            var processes = _processRegistry.All().Select(e => e.ProcessType.ProcessType).ToHashSet();

            foreach (var elem in all)
            {
                // Проверяем, что хендлер поддерживает тип процесса.
                if (!_staticInstanceHandler.CanProcess(elem))
                {
                    throw new InvalidOperationException($"{elem}");
                }

                // Прверяем, что процесс зарегистрирован.
                if (!processes.Contains(elem.ProcessType))
                {
                    throw new InvalidOperationException($"{elem}");
                }
            }
        }

        #endregion


        #region types

        public interface IQueries 
        {
            IContext PrepareContext(short deployVersion);

            Task CreateOrTryGetDeployWithLockAsync(
                IContext context,
                CancellationToken cancellationToken);

            Task<IReadOnlySet<RegistrationInfo>> LoadRegistrationsAsync(
                IContext context,
                CancellationToken cancellationToken);

            Task<ICollection<TId>> TryLockProcessesAsync(
                IContext context,
                ICollection<TId> processIds,
                CancellationToken cancellationToken);

            Task RemoveRegistrationsAsync(
                IContext context,
                ICollection<StaticInstanceProcessRegistrationDto> keys,
                CancellationToken cancellationToken);

            Task CreateRegistrationsAsync(
                IContext context,
                ICollection<RegistrationInfo> keys,
                CancellationToken cancellationToken
                );

            Task UpdateDeployVersionAsync(
                IContext context,
                CancellationToken cancellationToken);


            public interface IContext 
            {
                short DeployVersion { get; }

                DeployDto? DbDeploy { get; }

                bool DeployCreated { get; }
            }

            public readonly record struct DeployDto(
                short Version);

            public readonly record struct RegistrationInfo(
                StaticInstanceProcessRegistrationDto StaticInstanceRegistration,
                TId ProcessId
                )
            {
                public override int GetHashCode()
                {
                    return StaticInstanceRegistration.GetHashCode();
                }
            }
        }

        #endregion
    }
}
