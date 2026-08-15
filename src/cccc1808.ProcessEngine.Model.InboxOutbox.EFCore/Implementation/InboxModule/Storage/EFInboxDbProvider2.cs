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
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Components;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Storage
{
    public class EFInboxDbProvider2<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly InboxRegistryDto _inboxRegistry;

        private readonly EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options _repositoryOptions;

        private readonly IProcessLinkedConditions<TId, InboxProcessDataDbEntity<TId>> _processLinkedConditions;

        public EFInboxDbProvider2(
            IEFDbContext dbContext,
            InboxRegistryDto inboxRegistry,

            EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options repositoryOptions,

            IProcessLinkedConditions<TId, InboxProcessDataDbEntity<TId>> processLinkedConditions)
        {
            _dbContext = dbContext;
            _inboxRegistry = inboxRegistry;
            _repositoryOptions = repositoryOptions;
            _processLinkedConditions = processLinkedConditions;
        }

        public async Task LoadProcessDataAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            bool isAsyncExecution,
            CancellationToken cancellationToken)
        {
            if (!byTypeIndex.TryGetValue(_inboxRegistry.Unique.ProcessType, out var outboxProcessesIds))
            {
                return;
            }

            var data = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
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
                var component = new EFInboxComponentProxy<TId>(
                    processData,
                    Array.Empty<IInboxMessageComponent<TId>>() // [Info] В этой реализации собщения не загружаются заранее, только в самой обработке.
                    );
                process.AddComponent<IInboxComponent<TId>>(component);
                process.AddComponent<IStreamTriggerComponent>(
                    new StreamTriggerComponent(
                        _inboxRegistry.TriggerEventQueue,
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
