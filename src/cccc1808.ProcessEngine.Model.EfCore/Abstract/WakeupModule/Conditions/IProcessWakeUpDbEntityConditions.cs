using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Conditions
{
    public interface IProcessWakeUpDbEntityConditions<TId>
    {
        (
            object _no,
            IQueryableCondition<ProcessWakeUpDbEntity<TId>, ICollection<TId>> QueryRange
            ) ProcessLinkedDbEntity { get; }

        (
            IInMemoryCondition<ProcessWakeUpDbEntity<TId>> Memory,
            IQueryableCondition<ProcessWakeUpDbEntity<TId>> Query
            ) IsAsyncExecuting { get; }
    }
}
