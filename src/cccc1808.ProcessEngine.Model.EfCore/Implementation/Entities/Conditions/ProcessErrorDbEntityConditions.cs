using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Entities.Conditions
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
