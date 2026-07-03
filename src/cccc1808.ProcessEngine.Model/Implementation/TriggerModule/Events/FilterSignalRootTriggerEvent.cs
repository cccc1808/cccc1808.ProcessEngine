using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class FilterSignalRootTriggerEvent
        : TriggerEvent,
        IFilterSignalRootTriggerEvent
    {
        public ulong SignalCode { get; set; }

        [Obsolete]
        public FilterSignalRootTriggerEvent()
        { }

        public FilterSignalRootTriggerEvent(
            string triggerKey,
            ulong signalCode)
            : base(triggerKey, TriggerEventKindEnum.FilterSignalRootTriggerEvent)
        {
            SignalCode = signalCode;
        }
    }
}
