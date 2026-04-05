using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Conditions;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Conditions
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
