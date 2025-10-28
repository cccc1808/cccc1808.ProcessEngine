using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions
{
    public class ProcessInstanceInfoDto_Id_Condition<TId> 
        : IInMemoryProjectionCondition<ProcessInstanceInfoDto<TId>, ProcessIdDto<TId>>
    {
        public ProcessIdDto<TId> ApplayProjection(ProcessInstanceInfoDto<TId> source)
        {
            return source.Id;
        }

        public IEnumerable<ProcessIdDto<TId>> ApplayProjectionEnumerable(
            IEnumerable<ProcessInstanceInfoDto<TId>> source)
        {
            return source.Select(ApplayProjection);
        }
    }
}
