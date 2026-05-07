using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    public interface IProcessedOffsetTriggerEvent : ITriggerEvent
    {
        long ProcessedOffset { get; }
    }
}
