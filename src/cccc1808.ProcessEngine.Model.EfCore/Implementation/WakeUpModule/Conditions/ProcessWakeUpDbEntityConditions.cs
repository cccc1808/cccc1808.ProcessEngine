using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Conditions
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
            IInMemoryCondition<ProcessWakeUpDbEntity<TId>> Memory, 
            IQueryableCondition<ProcessWakeUpDbEntity<TId>> Query
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
                new DelegateInMemoryCondition<ProcessWakeUpDbEntity<TId>>((e) => e.IsAsyncExecuting),
                new DelegateIQueryableCondition<ProcessWakeUpDbEntity<TId>>((e) => e.Where(e => e.IsAsyncExecuting))
                );
        }
    }
}
