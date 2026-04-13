using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage
{
    /// <typeparam name="TId"></typeparam>
    public class EFWakeupDbProvider<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly IWakeupRegistry<TId> _wakeupRegistry;
        private readonly ProcessLinkedDbEntity_RangeCondition<TId, ProcessWakeupDbEntity<TId>> _processWakeUpDbEntity_ProcessId_RangeCondition;        

        public EFWakeupDbProvider(
            IEFDbContext dbContext,
            IWakeupRegistry<TId> wakeupRegistry)
        {
            _dbContext = dbContext;
            _wakeupRegistry = wakeupRegistry;
            _processWakeUpDbEntity_ProcessId_RangeCondition = new ProcessLinkedDbEntity_RangeCondition<TId, ProcessWakeupDbEntity<TId>>();
        }

        public async Task LoadProcessDataAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            bool isAsyncExecution,
            CancellationToken cancellationToken)
        {
            // Если это асинхронное выполнение, то компонент будет загружен по необходимости в IWakeupService.
            if (isAsyncExecution)
            {
                return;
            }

            var ids = byTypeIndex
                .Where(e => _wakeupRegistry.IsWakeupProcess(e.Key))
                .SelectMany(e => e.Value)
                .ToArray();

            var data = await _dbContext.Set<ProcessWakeupDbEntity<TId>>()
                .ApplayQueryCondition(_processWakeUpDbEntity_ProcessId_RangeCondition, ids)
                .ToArrayAsync(cancellationToken);

            foreach (var elem in data)
            {
                var process = processes[elem.ProcessId];
                process.AddComponent<IWakeupComponent>(
                    new EFWakeupProxyComponent<TId>(
                        elem));
            }
        }

        public Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            // [Hack]:
            // Если мы в асинхронном выполнении, то будет использоваться IProcessRepository<TId>.UpdateWakeupAsync
            // Инаече запись есть ChangeTracker и ничего дополнительно не требуется.

            return Task.CompletedTask;
        }
    }
}
