using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Entities.Conditions
{
    public class ProcessWakeUpDbEntityConditions<TId>
        : IProcessWakeUpDbEntityConditions<TId>
    {
        public (
            object _no,
            IQueryableCondition<ProcessWakeUpDbEntity<TId>, ICollection<TId>> QueryRange
            ) ProcessLinkedDbEntity
        { get; }

        public (
            IInMemoryCondition<ProcessWakeUpDbEntity<TId>, object?> Memory, 
            IQueryableCondition<ProcessWakeUpDbEntity<TId>, object?> Query
            ) IsAsyncExecuting
        { get; }

        public (
            IInMemoryCondition<ProcessWakeUpDbEntity<TId>, DateTimeOffset> Memory,
            IQueryableCondition<ProcessWakeUpDbEntity<TId>, (IEFDbContext dbContext, (TId processId, DateTimeOffset Date)[] Ids)> QueryRange
            ) IsAsyncExecuting_TimerDate 
        { get; }

        public ProcessWakeUpDbEntityConditions()
        {
            ProcessLinkedDbEntity = (
                null!, 
                new ProcessLinkedDbEntity_RangeCondition<TId, ProcessWakeUpDbEntity<TId>>());

            IsAsyncExecuting = (
                new DelegateInMemoryCondition<ProcessWakeUpDbEntity<TId>, object?>((e, _) => e.IsAsyncExecuting),
                new DelegateIQueryableCondition<ProcessWakeUpDbEntity<TId>, object?>((e, _) => e.Where(e => e.IsAsyncExecuting))
                );

            IsAsyncExecuting_TimerDate = (
                new DelegateInMemoryCondition<ProcessWakeUpDbEntity<TId>, DateTimeOffset>((e, p) => e.IsAsyncExecuting && e.TimerDate < p),
                new DelegateIQueryableCondition<ProcessWakeUpDbEntity<TId>, (IEFDbContext dbContext, (TId processId, DateTimeOffset Date)[] Ids)>(
                    (s, p) =>
                    {
                        var queryList = p.dbContext.QueryFromCollection(
                            p.Ids
                            .Select(e => new { ProcessId = e.processId, Date = e.Date })
                            .ToArray());

                        var query = 
                            from e1 in s
                            from e2 in queryList.Where(e2 =>
                                e1.ProcessId.Equals(e2.ProcessId)
                                && e1.IsAsyncExecuting
                                && e1.TimerDate <= e2.Date)
                            select e1;

                        return query;
                    }
                )
                );
        }
    }
}
