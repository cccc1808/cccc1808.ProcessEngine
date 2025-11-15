using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions;
using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.Implementation.Dto.Components.Conditions
{
    public class ProcessContainerConditions<TId> 
        : IProcessContainerConditions<TId>
    { 
        public (
            object? _no, 
            IInMemoryCondition<IProcessContainer<TId>, DateTimeOffset> Memory
            ) AsyncExecute
        { get; }

        public IInMemoryCondition<IProcessContainer<TId>, object?> HaveError { get; }

        public ProcessContainerConditions()
        {
            AsyncExecute = (
                null,
                new DelegateInMemoryCondition<IProcessContainer<TId>, DateTimeOffset>(
                    (s, p) => 
                        s.Process.Status == ProcessStatusEnum.AsyncExecute
                        && !s.Process.HaveErrorFlag
                        && s.Process.TimerDate < p
                    ));

            HaveError = new DelegateInMemoryCondition<IProcessContainer<TId>, object?>(
                (s, _) => s.Process.HaveErrorFlag || s.Process.ReTryCount.HasValue
                );
        }
    }
}
