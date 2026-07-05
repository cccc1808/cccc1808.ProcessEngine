using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class SignalFilterRootTriggerEvent
        : TriggerEvent,
        IFilterSignalRootTriggerEvent
    {
        public ulong SignalCodeFilter { get; set; }

        [Obsolete]
        public SignalFilterRootTriggerEvent()
        { }

        public SignalFilterRootTriggerEvent(
            string triggerKey,
            ulong signalCode)
            : base(triggerKey, TriggerEventKindEnum.SignalFilterRootTriggerEvent)
        {
            SignalCodeFilter = signalCode;
        }
    }
}
