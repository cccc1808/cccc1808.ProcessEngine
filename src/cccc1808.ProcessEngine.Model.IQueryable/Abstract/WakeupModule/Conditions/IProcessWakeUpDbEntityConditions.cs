using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;

namespace cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Conditions
{
    public interface IProcessWakeupDbEntityConditions<TId>
    {
        (
            object _no,
            IQueryableCondition<ProcessWakeupDbEntity<TId>, ICollection<TId>> QueryRange
            ) ProcessLinkedDbEntity { get; }

        (
            IInMemoryCondition<ProcessWakeupDbEntity<TId>> Memory,
            IQueryableCondition<ProcessWakeupDbEntity<TId>> Query
            ) IsAsyncExecuting { get; }
    }
}
