using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Test2.Infrastructure.ParentChild.Entities;
using cccc1808.ProcessEngine.Test2.Infrastructure.ParentChildModule.Dto;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test2.Infrastructure.ParentChildModule.Storage
{
    internal class ParentChildDbProvider
        : IProcessDbProvider<Guid>
    {
        private readonly IReadOnlySet<ProcessTypeDto> _registrations;
        private readonly IEFDbContext _dbContext;

        public ParentChildDbProvider(
            IEnumerable<ChildRegistrationDto> registrations, 
            IEFDbContext dbContext)
        {
            _registrations = registrations.Select(e => e.ProcessType).ToHashSet();
            _dbContext = dbContext;
        }

        public async Task LoadProcessDataAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes, 
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            bool isAsyncExecution,
            CancellationToken cancellationToken)
        {
            var forProcessing = byTypeIndex
                .Where(e => _registrations.Contains(e.Key))
                .SelectMany(e => e.Value)
                .Select(e => processes[e])
                .ToArray();

            if (!forProcessing.Any())
            {
                return;
            }

            var data = await _dbContext.Set<ParentChildProcessDbEntity>()
                .Where(e => forProcessing.Select(e => e.Process.Info.Id).Contains(e.ChildProcessId))
                .ToArrayAsync(cancellationToken);

            foreach (var elem in data)
            {
                var process = processes[elem.ChildProcessId];
                process.AddComponent(elem);
            }
        }

        public Task UpdateAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes,
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex, 
            CancellationToken cancellationToken)
        {
            // EF
            return Task.CompletedTask;
        }
    }
}
