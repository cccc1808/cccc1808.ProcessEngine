using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions
{
    public class ProcessIdDto_Id_Condition<TId, TProcess>
        : IInMemoryProjectionCondition<ProcessIdDto<TId>, TId>
    {
        public TId ApplayProjection(ProcessIdDto<TId> source)
        {
            return source.Id;
        }

        public IEnumerable<TId> ApplayProjectionEnumerable(IEnumerable<ProcessIdDto<TId>> source)
        {
            return source.Select(ApplayProjection);
        }
    }
}
