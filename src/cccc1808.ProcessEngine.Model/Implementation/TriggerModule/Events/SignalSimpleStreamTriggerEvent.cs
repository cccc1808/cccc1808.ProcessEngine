using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class SignalSimpleStreamTriggerEvent :
        TriggerEvent,
        ISignalSimpleStreamTriggerEvent
    {
        [Obsolete("Сериализатор.")]
        public SignalSimpleStreamTriggerEvent()
        { }

        public SignalSimpleStreamTriggerEvent(
            string triggerKey)
            : base(
                  triggerKey,
                  TriggerEventKindEnum.SimpleStreamEvent)
        {
        }
    }
}
