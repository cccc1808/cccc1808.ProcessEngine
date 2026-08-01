using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Conditions
{
    public class TriggerComponentCondition<TId> 
        : ITriggerComponentCondition<TId>
    {
        public IInMemoryCondition<ITriggerComponent<TId>, ITriggerComponentCondition<TId>.NeedExecuteParameters> NeedExecuteCondition { get; }

        public TriggerComponentCondition()
        {
            NeedExecuteCondition = new DelegateInMemoryCondition<ITriggerComponent<TId>, ITriggerComponentCondition<TId>.NeedExecuteParameters>(
                static (e, p) => 
                {
                    if (e.IsCompleted)
                    {
                        return false;
                    }

                    if (!e.IsActivated)
                    {
                        return false;
                    }

                    if (e.TimerDate > p.TimeoutNowDate)
                    {
                        return false;
                    }

                    if (e.ChildTrigger is not null
                        && e.ChildTrigger.WaitDeliveryTimestamp.HasValue)
                    {
                        return false;
                    }

                    return true;
                });
        }
    }
}
