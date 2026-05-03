using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events.Stream
{
    public class SignalSimpleStreamTriggerEvent
        : TriggerEvent,
        ISignalSimpleStreamTriggerEvent
    {
        [Obsolete("Сериализатор.")]
        public SignalSimpleStreamTriggerEvent()
        {
        }

        public SignalSimpleStreamTriggerEvent(string triggerKey)
            : base(
                  triggerKey, 
                  ignoreDelay: false, 
                  ITriggerEvent.KindEnum.SimpleStream_SignalEvent) 
        { }
    }
}
