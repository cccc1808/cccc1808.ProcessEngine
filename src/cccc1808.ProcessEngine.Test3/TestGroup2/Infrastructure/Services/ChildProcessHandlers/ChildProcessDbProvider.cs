using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Async;

namespace cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services
{
    internal class ChildProcessDbProvider
        : IProcessDbProvider<Guid>
    {
        private readonly ILinq2DbDataConnection _dbDataConnection;

        public ChildProcessDbProvider(
            ILinq2DbDataConnection dbDataConnection)
        {
            _dbDataConnection = dbDataConnection;
        }

        public async Task LoadProcessDataAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes, 
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            bool isAsyncExecution,
            CancellationToken cancellationToken)
        {
            var typedProcesses = byTypeIndex.TryGetValue(new ProcessTypeDto(4, 1), out var group)
                ? group
                    .Select(e => processes[e])
                    .ToArray()
                : [];

            var data = await _dbDataConnection.Set<ChildProcessDbEntity>()
                .Where(e => typedProcesses.Select(e => e.Id).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            foreach (var elem in typedProcesses)
            {
                elem.AddComponent(data[elem.Id]);
            }
        }

        public async Task UpdateAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes, 
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var typedProcesses = byTypeIndex.TryGetValue(new ProcessTypeDto(4, 1), out var group)
                ? group
                    .Select(e => processes[e])
                    .ToArray()
                : [];

            var entities = typedProcesses
                .Select(
                    e => e.TryGetComponent<ChildProcessDbEntity>(out var component)
                    ? (component, true)
                    : (component, false))
                .Where(e => e.Item2)
                .Select(e => e.Item1)
                .ToArray();

            await _dbDataConnection.Set<ChildProcessDbEntity>()
                .Merge()
                .Using(entities)
                .OnTargetKey()
                .UpdateWhenMatched()
                .MergeAsync(cancellationToken);
        }
    }
}
