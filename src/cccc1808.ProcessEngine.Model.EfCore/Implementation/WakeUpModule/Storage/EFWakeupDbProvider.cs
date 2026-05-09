using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Components;

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
                .Where(e => _wakeupRegistry.CheckWakeup(e.Key) == WakeupStateEnum.CheckWakeupWithLock)
                .SelectMany(e => e.Value)
                .ToArray();

            var data = await _dbContext.Set<ProcessWakeupDbEntity<TId>>()
                .ApplayQueryCondition(_processWakeUpDbEntity_ProcessId_RangeCondition, ids)
                .ToArrayAsync(cancellationToken);

            foreach (var elem in data)
            {
                var process = processes[elem.ProcessId];
                process.AddComponent<IWakeupComponent<TId>>(
                    new WakeupComponent<TId>(
                        elem.Id,
                        elem.IsAsyncExecuting,
                        haveWakeupEntity: true,
                        needUpdate: false));
            }
        }

        public Task UpdateAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            // Если это асинхронное выполнение, то компонент будет обновлен.
            if (!processes.Any())
            {
                return Task.CompletedTask;
            }

            if (processes.Values.First().InAsyncExecuting)
            {
                return Task.CompletedTask;
            }

            // Иначе обновляем в ChangeTracker.
            var ids = byTypeIndex
                .Where(e => _wakeupRegistry.CheckWakeup(e.Key) == WakeupStateEnum.CheckWakeupWithLock)
                .SelectMany(e => e.Value)
                .ToArray();

            foreach (var elem in ids.Select(e => processes[e]))
            {
                if (!elem.TryGetComponent<IWakeupComponent<TId>>(out var component))
                {
                    continue;
                }
                
                if (!component.NeedUpdate)
                {
                    continue;
                }

                var entry = _dbContext.AttachEntity(
                    new ProcessWakeupDbEntity<TId>(
                        component.Id,
                        elem.Id,
                        component.IsAsyncExecuting),
                        throwIfAttached: false);
                entry.Entity.IsAsyncExecuting = component.IsAsyncExecuting;
            }

            return Task.CompletedTask;
        }
    }
}
