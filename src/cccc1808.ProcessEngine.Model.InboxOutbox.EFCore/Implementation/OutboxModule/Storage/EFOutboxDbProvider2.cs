using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Components;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage
{
    /// <summary>
    /// Особенность: загружает процесс, данные, не загружает сообщения.
    /// </summary>
    public class EFOutboxDbProvider2<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly OutboxRegistryDto _outboxRegistry;

        private readonly EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options _repositoryOptions;

        private readonly IProcessLinkedConditions<TId, OutboxProcessDataDbEntity<TId>> _processLinkedConditions;

        public EFOutboxDbProvider2(
            IEFDbContext dbContext, 
            OutboxRegistryDto outboxRegistry,
            
            EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options repositoryOptions, 
            
            IProcessLinkedConditions<TId, OutboxProcessDataDbEntity<TId>> processLinkedConditions)
        {
            _dbContext = dbContext;
            _outboxRegistry = outboxRegistry;
            _repositoryOptions = repositoryOptions;
            _processLinkedConditions = processLinkedConditions;
        }

        public async Task LoadProcessDataAsync(
            IDictionary<TId, IProcessContainer<TId>> processes, 
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex, 
            bool isAsyncExecution, 
            CancellationToken cancellationToken)
        {
            if (!byTypeIndex.TryGetValue(_outboxRegistry.Unique.ProcessType, out var outboxProcessesIds))
            {
                return;
            }
            
            var data = await _dbContext.Set<OutboxProcessDataDbEntity<TId>>()
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, outboxProcessesIds)
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            var softTimeout = _repositoryOptions.SoftTimeout.HasValue
                ? DateTimeOffset.Now + _repositoryOptions.SoftTimeout.Value
                : (DateTimeOffset?)null;

            foreach (var elem in outboxProcessesIds)
            {
                var process = processes[elem];
                var processData = data[elem];

                if (softTimeout.HasValue)
                {
                    process.AddComponent<ISoftTimeoutComponent>(
                        new SoftTimeoutComponent(softTimeout));
                }
                var component = new EFOutboxComponentProxy<TId>(
                    processData,
                    Array.Empty<IOutboxMessageComponent<TId>>() // [Info] В этой реализации собщения не загружаются заранее, только в самой обработке.
                    );
                process.AddComponent<IOutboxComponent<TId>>(component);
                process.AddComponent<IStreamTriggerComponent>(
                    new StreamTriggerComponent(
                        _outboxRegistry.TriggerEventQueue,
                        [processData.WakeupTriggerKey]));
            }
        }

        public Task UpdateAsync(
            IDictionary<TId, IProcessContainer<TId>> processes, 
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex, 
            CancellationToken cancellationToken)
        {
            // EF дополнительное сохранение не нужно.
            return Task.CompletedTask;
        }
    }
}
