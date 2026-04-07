using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerSetter<TId> : ITriggerSetter<TId>
    {
        public bool IsCounterActivated(ITriggerComponent<TId> trigger)
        {
            return trigger.Counter.Value <= 0;
        }

        public void ProcessCounter(ITriggerComponent<TId> trigger, int eventCount)
        {
            trigger.Counter -= eventCount;
        }

        public void SetActivated(ITriggerComponent<TId> trigger, bool value)
        {
            trigger.IsActivated = value;
        }

        public void SetCompleted(ITriggerComponent<TId> trigger, bool value)
        {
            trigger.IsCompleted = value;
        }

        public void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value)
        {
            trigger.TimerDate = value;
        }
    }
}
