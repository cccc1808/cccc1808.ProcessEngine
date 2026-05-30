using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Implementation.Common.Conditions;

namespace cccc1808.ProcessEngine.Model.IQueryable.ProcessModule.Conditions
{
    public class ProcessErrorDbEntityConditions<TId> : IProcessErrorDbEntityConditions<TId>
    {        

        public (
            object? _no,
            IQueryableCondition<ProcessErrorDbEntity<TId>, ICollection<TId>> QueryRange
            ) ProcessLinkedDbEntity
        { get; }

        public ProcessErrorDbEntityConditions()
        {
            ProcessLinkedDbEntity = (
                null, 
                new ProcessLinkedDbEntity_RangeCondition<TId, ProcessErrorDbEntity<TId>>()
                );
        }
    }
}
