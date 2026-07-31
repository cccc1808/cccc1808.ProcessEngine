using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services.RootTrigger
{
    public class RootTriggerDbProvider : IProcessDbProvider<Guid>
    {
        private readonly IEFDbContext _dbContext;
        private readonly TriggerRunner<Guid>.OptionsDto _triggerOptions;

        public RootTriggerDbProvider(
            IEFDbContext dbContext, 
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

        public Task UpdateAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes,
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
