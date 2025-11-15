using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions
{
    public interface IProcessWakeUpDbEntityConditions<TId>
    {
        (
            object _no,
            IQueryableCondition<ProcessWakeUpDbEntity<TId>, ICollection<TId>> QueryRange
            ) ProcessLinkedDbEntity { get; }

        (
            IInMemoryCondition<ProcessWakeUpDbEntity<TId>, object?> Memory,
            IQueryableCondition<ProcessWakeUpDbEntity<TId>, object?> Query
            ) IsAsyncExecuting { get; }

        (
            IInMemoryCondition<ProcessWakeUpDbEntity<TId>, DateTimeOffset> Memory,
            IQueryableCondition<ProcessWakeUpDbEntity<TId>, (IEFDbContext dbContext, (TId processId, DateTimeOffset Date)[] Ids)> QueryRange
            ) IsAsyncExecuting_TimerDate { get;}
    }
}
