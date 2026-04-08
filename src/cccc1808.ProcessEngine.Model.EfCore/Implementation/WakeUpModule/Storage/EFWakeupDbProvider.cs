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
            var ids = byTypeIndex
                .Where(e => _wakeupRegistry.IsWakeupProcess(e.Key))
                .SelectMany(e => e.Value)
                .ToArray();

            // Не блокируем т.к. система отдельно управляет блокировками.
            var data = await _dbContext.Set<ProcessWakeupDbEntity<TId>>()
                .AsNoTracking() // [Hack]: Не отслеживаем, смотри IProcessRepository<TId>.UpdateWakeupAsync
                .ApplayQueryCondition(_processWakeUpDbEntity_ProcessId_RangeCondition, ids)
                .ToArrayAsync(cancellationToken);

            // TODO: подумать: в текущей реализации как будто вообще нет смысла предзагружать wakeup компонент.
            // * В change tracker его нет (он не откатиться в случае использования ChangeTrackerIsolation)
            // * Он не изменяется в асинхронной обработке до этапа пробуждения.
            // Только если добавить процессу возможность вручную подавить wakeup check в текущей сессии, но это вроде не особо нужно.
            // * Его можно загружать уже в самом waleup компоненте.
            foreach (var elem in data)
            {
                var process = processes[elem.ProcessId];

                process.AddComponent<IWakeupComponent>(
                    new EFWakeupProxyComponent<TId>(
                        elem, 
                        inAsyncExecuting: true));
            }
        }

        public Task LoadProcessForAsyncProcessingAsync(
            IDictionary<TId, ProcessInstanceInfoDto<TId>> notLoadedProcesses, 
            IDictionary<TId, IProcessContainer<TId>> loadBuffer, 
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            // Без кастомного загрузчика.
            return Task.CompletedTask;
        }

        public async Task LoadRangeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            bool withLock,
            CancellationToken cancellationToken)
        {
            // Не блокируем при withLock.
            var data = await _dbContext.Set<ProcessWakeupDbEntity<TId>>()
                .ApplayQueryCondition(_processWakeUpDbEntity_ProcessId_RangeCondition, processes.Keys)
                .ToArrayAsync(cancellationToken);

            foreach (var elem in data)
            {
                var process = processes[elem.ProcessId];

                process.AddComponent<IWakeupComponent>(
                    new EFWakeupProxyComponent<TId>(
                        elem,
                        inAsyncExecuting: true));
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
