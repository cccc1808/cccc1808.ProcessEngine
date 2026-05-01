using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services
{
    internal class ParentCheckWakeupHandler
        : IWakeupCheckHandler<Guid>
    {
        private readonly IEFDbContext _dbContext;

        public ParentCheckWakeupHandler(IEFDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async ValueTask<IDictionary<Guid, bool>> HandleRangeAsync(
            ICollection<IProcessContainer<Guid>> processes, 
            CancellationToken cancellationToken)
        {
            var ids = processes.Select(e => (Guid?)e.Id);

            // Проверяем наличие незавершенных дочерних процессов.
            var activeChildExsist = await _dbContext.Set<ChildProcessDbEntity>()
                .Where(e => 
                    e.ActiveParentProcessId.HasValue
                    && ids.Contains(e.ActiveParentProcessId)
                    )
                .GroupBy(e => e.ActiveParentProcessId)
                .Select(e => new { e.Key, Any = e.Any() })
                .ToDictionaryAsync(e => e.Key, e => e.Any, cancellationToken);

            var result = new Dictionary<Guid, bool>(processes.Count);
            foreach (var elem in processes)
            {
                if (activeChildExsist.TryGetValue(elem.Id, out var exsists))
                {
                    result.Add(elem.Id, !exsists);
                }
                else
                {
                    result.Add(elem.Id, true);
                }
            }
            return result;
        }
    }
}
