using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class SignalOffsetTriggerEvent :
        TriggerEvent,
        ISignalOffsetTriggerEvent
    {
        public long UpdateOffset { get; set; }

        [Obsolete("Сериализатор.")]
        public SignalOffsetTriggerEvent() 
        { }

        public SignalOffsetTriggerEvent(
            string triggerKey,
            long updateOffset)
            : base(
                  triggerKey,
                  TriggerEventKindEnum.SignalOffsetEvent)
        {
            UpdateOffset = updateOffset;
        }
    }
}
