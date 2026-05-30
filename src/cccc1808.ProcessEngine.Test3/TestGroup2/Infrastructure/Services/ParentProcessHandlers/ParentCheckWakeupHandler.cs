using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB.Async;

namespace cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services
{
    internal class ParentCheckWakeupHandler
        : IWakeupCheckHandler<Guid>
    {
        private readonly ILinq2DbDataConnection _dbDataConnection;

        public ParentCheckWakeupHandler(
            ILinq2DbDataConnection dbDataConnection)
        {
            _dbDataConnection = dbDataConnection;
        }

        public async ValueTask<IDictionary<Guid, bool>> HandleRangeAsync(
            ICollection<IProcessContainer<Guid>> processes, 
            CancellationToken cancellationToken)
        {
            var ids = processes.Select(e => (Guid?)e.Id);

            // Проверяем наличие незавершенных дочерних процессов.
            var activeChildExsist = await _dbDataConnection.Set<ChildProcessDbEntity>()
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
