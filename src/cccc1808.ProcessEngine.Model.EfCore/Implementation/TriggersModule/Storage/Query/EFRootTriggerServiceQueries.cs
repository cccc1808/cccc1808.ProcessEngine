using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query
{
    public class EFRootTriggerServiceQueries<TId> 
        : IRootTriggerService<TId>.IQueries
    {
        private readonly IEFDbContext _dbContext;

        public EFRootTriggerServiceQueries(
            IEFDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ICollection<(TId ProcessId, string Key)>> GetChildTriggersForRootTriggerProcessGoSleepAsyncAsync(
            ICollection<ITriggerComponent<TId>> rootTriggers,
            CancellationToken cancellationToken)
        {
            var rootKeys = rootTriggers
                .Select(e => e.Key)
                .ToArray();

            // TODO: condition;
            var data = await _dbContext.Set<TriggerDbEntity<TId>>()
                .Where(
                    e =>
                    rootKeys.Contains(e.RootTriggerKey)
                    // Только этим типам триггеров нужно оповещение о том, что процесс уснул.
                    && (e.Kind == ITriggerComponent.TriggerKind.SimpleStream
                    || e.Kind == ITriggerComponent.TriggerKind.OffsetStream)
                    // Для оптимизации
                    && !e.IsCompleted
                    )
                .Select(e => new { e.ProcessId, e.Key })
                .ToArrayAsync(cancellationToken);

            return data
                .Select(e => (e.ProcessId, e.Key))
                .ToArray();
        }
    }
}
