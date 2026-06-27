using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Storage.Queries
{
    public class EFTokenExecutionServiceQueries<TId>
        : TokenExecutionService<TId>.IQueries
    {
        private readonly IEFDbContext _dbContext;

        public EFTokenExecutionServiceQueries(
            IEFDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string[]> GetStreamTriggerKeysByProcessRangeAsync(
            TId processId,
            CancellationToken cancellationToken)
        {
            ITriggerComponent.TriggerKind[] streamKinds = [
                ITriggerComponent.TriggerKind.SimpleStream, 
                ITriggerComponent.TriggerKind.SimpleStreamRoot, 
                ITriggerComponent.TriggerKind.OffsetStream
                ];
            
            var result = await _dbContext.Set<TriggerDbEntity<TId>>()
                .Where(
                    e => e.ProcessId.Equals(processId)
                        && streamKinds.Contains(e.Kind)
                        && !e.IsCompleted)
                .Select(e => e.Key)
                .ToArrayAsync(cancellationToken);

            return result;
        }
    }
}
