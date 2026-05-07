using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Implementation.Common.Conditions;

namespace cccc1808.ProcessEngine.Model.IQueryable.Implementation.WakeUpModule.Conditions
{
    public class ProcessWakeupDbEntityConditions<TId>
        : IProcessWakeupDbEntityConditions<TId>
    {
        public (
            object _no,
            IQueryableCondition<ProcessWakeupDbEntity<TId>, ICollection<TId>> QueryRange
            ) ProcessLinkedDbEntity
        { get; }

        public (
            IInMemoryCondition<ProcessWakeupDbEntity<TId>> Memory, 
            IQueryableCondition<ProcessWakeupDbEntity<TId>> Query
            ) IsAsyncExecuting
        { get; }

        public ProcessWakeupDbEntityConditions()
        {
            ProcessLinkedDbEntity = (
                null!, 
                new ProcessLinkedDbEntity_RangeCondition<TId, ProcessWakeupDbEntity<TId>>());

            IsAsyncExecuting = (
                new DelegateInMemoryCondition<ProcessWakeupDbEntity<TId>>((e) => e.IsAsyncExecuting),
                new DelegateIQueryableCondition<ProcessWakeupDbEntity<TId>>((e) => e.Where(e => e.IsAsyncExecuting))
                );
        }
    }
}
