using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions
{
    /// <summary>
    /// IProcessEntity<TId>.Info.Id.Id
    /// </summary>
    public class IProcessContainer_Id_Condition<TId>
        : IInMemoryProjectionCondition<IProcessContainer<TId>, TId>
    {
        public TId ApplayProjection(IProcessContainer<TId> source)
        {
            return source.Id;
        }

        public IEnumerable<TId> ApplayProjectionEnumerable(
            IEnumerable<IProcessContainer<TId>> source)
        {
            return source.Select(ApplayProjection);
        }        
    }
}
