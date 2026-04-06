using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.ChildProcess
{
    internal class ChildProcessDbProvider
        : IProcessDbProvider<Guid>
    {
        private readonly IEFDbContext _efDbContext;

        public ChildProcessDbProvider(
            IEFDbContext efDbContext)
        {
            _efDbContext = efDbContext;
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes, 
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var typedProcesses = byTypeIndex.TryGetValue(new ProcessTypeDto(4, 1), out var group)
                ? group
                    .Select(e => processes[e])
                    .ToArray()
                : [];

            var data = await _efDbContext.Set<ChildProcessDbEntity>()
                .Where(e => typedProcesses.Select(e => e.Id).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            foreach (var elem in typedProcesses)
            {
                elem.AddComponent(data[elem.Id]);
            }
        }

        public async Task LoadRangeAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes,
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            bool withLock,
            CancellationToken cancellationToken)
        {
            var typedProcesses = byTypeIndex.TryGetValue(new ProcessTypeDto(4, 1), out var group)
                ? group
                    .Select(e => processes[e])
                    .ToArray()
                : [];

            var data = await _efDbContext.Set<ChildProcessDbEntity>()
                .Where(e => typedProcesses.Select(e => e.Id).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            foreach (var elem in typedProcesses)
            {
                elem.AddComponent(data[elem.Id]);
            }
        }

        public Task UpdateAsync(
            ICollection<IProcessContainer<Guid>> processes, 
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
