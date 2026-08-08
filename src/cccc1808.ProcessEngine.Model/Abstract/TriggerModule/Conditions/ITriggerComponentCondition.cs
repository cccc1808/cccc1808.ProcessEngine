using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Conditions
{
    public interface ITriggerComponentCondition<TId>
    {
        public IInMemoryCondition<ITriggerComponent<TId>, NeedExecuteParameters> NeedExecuteCondition { get; }

        public readonly record struct NeedExecuteParameters(
            DateTimeOffset TimeoutNowDate);
    }
}
