using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions
{
    public class IProcessContainer_ProcessInstanceInfoDto_Condition<TId, TProcess>
        : IInMemoryProjectionCondition<TProcess, ProcessInstanceInfoDto<TId>>        
        where TProcess : IProcessContainer<TId>
    {
        public ProcessInstanceInfoDto<TId> ApplayProjection(TProcess source)
        {
            return source.Process.Info;
        }

        public IEnumerable<ProcessInstanceInfoDto<TId>> ApplayProjectionEnumerable(
            IEnumerable<TProcess> source)
        {
            return source.Select(ApplayProjection);
        }        
    }
}
