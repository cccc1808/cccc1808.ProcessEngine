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
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Implementation.Common.Conditions;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Async;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.WakeUpModule.Storage
{
    /// <typeparam name="TId"></typeparam>
    public class Linq2DbWakeupDbProvider<TId>
        : IProcessDbProvider<TId>
    {
        private readonly ILinq2DbDataConnection _dataConnection;
        private readonly IWakeupRegistry<TId> _wakeupRegistry;
        private readonly ProcessLinkedDbEntity_RangeCondition<TId, ProcessWakeupDbEntity<TId>> _processWakeUpDbEntity_ProcessId_RangeCondition;        

        public Linq2DbWakeupDbProvider(
            ILinq2DbDataConnection dataConnection,
            IWakeupRegistry<TId> wakeupRegistry)
        {
            _dataConnection = dataConnection;
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

            if (!ids.Any())
            {
                return;
            }

            var data = await _dataConnection.Set<ProcessWakeupDbEntity<TId>>()
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

        public async Task UpdateAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            // Если это асинхронное выполнение, то компонент будет обновлен.
            if (!processes.Any())
            {
                return;
            }

            if (processes.Values.First().InAsyncExecuting)
            {
                return;
            }

            // Иначе обновляем в ChangeTracker.
            var ids = byTypeIndex
                .Where(e => _wakeupRegistry.CheckWakeup(e.Key) == WakeupStateEnum.CheckWakeupWithLock)
                .SelectMany(e => e.Value)
                .ToArray();

            var asyncExecuting = new List<TId>(ids.Length);
            var noAsyncExecuting = new List<TId>(ids.Length);
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

                if (elem.InAsyncExecuting)
                {
                    asyncExecuting.Add(elem.Id);
                }
                else 
                {
                    noAsyncExecuting.Add(elem.Id);
                }
            }

            if (asyncExecuting.Any())
            {
                await _dataConnection.Set<ProcessWakeupDbEntity<TId>>()
                    .Where(e => asyncExecuting.Contains(e.Id))
                    .Set(e => e.IsAsyncExecuting, true)
                    .UpdateAsync(cancellationToken);
            }
            if (noAsyncExecuting.Any())
            {
                await _dataConnection.Set<ProcessWakeupDbEntity<TId>>()
                    .Where(e => noAsyncExecuting.Contains(e.Id))
                    .Set(e => e.IsAsyncExecuting, false)
                    .UpdateAsync(cancellationToken);
            }
        }
    }
}
