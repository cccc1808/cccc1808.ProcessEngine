using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Components;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage.DbProvider
{
    public class EFWakeupDbProvider<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly ProcessTypeDto[] _registrys;
        private readonly ProcessLinkedDbEntity_RangeCondition<TId, ProcessWakeUpDbEntity<TId>> _processWakeUpDbEntity_ProcessId_RangeCondition;        

        public EFWakeupDbProvider(
            IEFDbContext dbContext,
            IEnumerable<WakeupRegistryDto> registrys)
        {
            _dbContext = dbContext;
            _registrys = registrys.Select(e => e.ProcessRegistry.ProcessType).ToArray();
            _processWakeUpDbEntity_ProcessId_RangeCondition = new ProcessLinkedDbEntity_RangeCondition<TId, ProcessWakeUpDbEntity<TId>>();
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var ids = byTypeIndex
                .Where(e => _registrys.Contains(e.Key))
                .SelectMany(e => e.Value)
                .ToArray();

            // Не блокируем т.к. система отдельно управляет блокировками.
            var data = await _dbContext.Set<ProcessWakeUpDbEntity<TId>>()
                .ApplayFilterCondition(_processWakeUpDbEntity_ProcessId_RangeCondition, ids)
                .ToArrayAsync(cancellationToken);

            foreach (var elem in data)
            {
                var process = processes[elem.ProcessId];

                process.AddComponent(
                    new EFWakeUpProxyComponent<TId>(elem, inAsyncExecuting: true));
            }
        }

        public async Task LoadRangeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            bool withLock,
            CancellationToken cancellationToken)
        {
            // Не блокируем при withLock.
            var data = await _dbContext.Set<ProcessWakeUpDbEntity<TId>>()
                .ApplayFilterCondition(_processWakeUpDbEntity_ProcessId_RangeCondition, processes.Keys)
                .ToArrayAsync(cancellationToken);

            foreach (var elem in data)
            {
                var process = processes[elem.ProcessId];

                process.AddComponent<IWakeUpComponent>(
                    new EFWakeUpProxyComponent<TId>(elem, inAsyncExecuting: false));
            }
        }

        public Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            // EF change tracker.
            foreach (var elem in processes)
            {
                elem.GetComponent<IWakeUpComponent>().NeedUpdate = false;
            }

            return Task.CompletedTask;
        }
    }
}
