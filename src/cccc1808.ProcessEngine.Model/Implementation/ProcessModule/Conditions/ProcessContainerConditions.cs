using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Conditions
{
    public class ProcessContainerConditions<TId> 
        : IProcessContainerConditions<TId>
    { 
        public (
            object? _no, 
            IInMemoryCondition<IProcessContainer<TId>> Memory
            ) AsyncExecute
        { get; }

        public IInMemoryCondition<IProcessContainer<TId>> HaveError { get; }

        public ProcessContainerConditions()
        {
            AsyncExecute = (
                null,
                new DelegateInMemoryCondition<IProcessContainer<TId>>(
                    (s) => 
                        s.Process.Status == ProcessStatusEnum.AsyncExecute
                    ));

            HaveError = new DelegateInMemoryCondition<IProcessContainer<TId>>(
                (s) =>   s.Process.StoppedByError || s.Process.RetryCount.HasValue
                );
        }
    }
}
