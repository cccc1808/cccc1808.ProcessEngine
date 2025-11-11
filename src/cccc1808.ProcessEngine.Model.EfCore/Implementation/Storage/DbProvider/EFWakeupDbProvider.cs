using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Dto.Components;
using cccc1808.ProcessEngine.Model.Implementation.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage.DbProvider
{
    public class EFWakeupDbProvider<TId, TDbContext>
        : IProcessDbProvider<TId>
        where TDbContext: DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly ProcessWakeUpDbEntity_ProcessId_RangeCondition<TId> _processWakeUpDbEntity_ProcessId_RangeCondition;

        public EFWakeupDbProvider(
            TDbContext dbContext)
        {
            _dbContext = dbContext;
            _processWakeUpDbEntity_ProcessId_RangeCondition = new ProcessWakeUpDbEntity_ProcessId_RangeCondition<TId>();
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            // Не блокируем т.к. система отдельно управляет блокировками.
            var data = await _dbContext.Set<ProcessWakeUpDbEntity<TId>>()
                // TODO: filter process
                .ApplayFilterCondition(_processWakeUpDbEntity_ProcessId_RangeCondition, processes.Keys)
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
