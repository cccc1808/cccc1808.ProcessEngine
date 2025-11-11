using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions
{
    public class ProcessWakeUpDbEntity_ProcessId_RangeCondition<TId>
        : IQueryableCondition<ProcessWakeUpDbEntity<TId>, ICollection<TId>>
    {
        public IQueryable<ProcessWakeUpDbEntity<TId>> ApplayQueryable(
            IQueryable<ProcessWakeUpDbEntity<TId>> source, 
            ICollection<TId> parameters)
        {
            return source
                .Where(e => parameters.Contains(e.ProcessId));
        }
    }
}
