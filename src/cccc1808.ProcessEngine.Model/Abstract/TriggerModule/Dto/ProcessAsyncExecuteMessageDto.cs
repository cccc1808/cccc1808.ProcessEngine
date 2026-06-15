using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto
{
    public readonly record struct ProcessAsyncExecuteMessageDto<TId>(
        ProcessInstanceInfoDto<TId> ProcessInstanceInfo,
        bool IsRangeProcess
        );
}
