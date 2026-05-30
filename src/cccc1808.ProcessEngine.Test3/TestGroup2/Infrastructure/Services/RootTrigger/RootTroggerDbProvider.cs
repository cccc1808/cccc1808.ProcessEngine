using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Async;

namespace cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services.RootTrigger
{
    public class RootTroggerDbProvider : IProcessDbProvider<Guid>
    {
        private readonly ILinq2DbDataConnection _dbContext;
        private readonly TriggerRunner<Guid>.OptionsDto _triggerOptions;

        public RootTroggerDbProvider(
            ILinq2DbDataConnection dbContext, 
            TriggerRunner<Guid>.OptionsDto triggerOptions)
        {
            _dbContext = dbContext;
            _triggerOptions = triggerOptions;
        }

        public async Task LoadProcessDataAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes, 
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            bool isAsyncExecution,
            CancellationToken cancellationToken)
        {
            var typedProcesses = byTypeIndex.TryGetValue(new ProcessTypeDto(5, 1), out var group)
                ? group
                    .Select(e => processes[e])
                    .ToArray()
                : [];

            var data = await _dbContext.Set<RootTriggerDbEntity>()
                .Where(e => typedProcesses.Select(e => e.Id).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            foreach (var elem in typedProcesses)
            {
                elem.AddComponent(data[elem.Id]);
                elem.AddComponent<IStreamTriggerComponent>(
                    new StreamTriggerComponent(
                        _triggerOptions.TriggerEventQueues.Single().QueueName,
                        []
                        )
                    );
            }
        }

        public async Task UpdateAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes,
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var typedProcesses = byTypeIndex.TryGetValue(new ProcessTypeDto(5, 1), out var group)
                ? group
                    .Select(e => processes[e])
                    .ToArray()
                : [];

            var entities = typedProcesses
                .Select(
                    e => e.TryGetComponent<RootTriggerDbEntity>(out var component)
                    ? (component, true)
                    : (component, false))
                .Where(e => e.Item2)
                .Select(e => e.Item1)
                .ToArray();

            await _dbContext.Set<RootTriggerDbEntity>()
                .Merge()
                .Using(entities)
                .OnTargetKey()
                .UpdateWhenMatched()
                .MergeAsync(cancellationToken);
        }
    }
}
