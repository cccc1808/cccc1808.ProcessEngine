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
        public string? SendTriggerKey { get; set; }

        public long? SendTimeStamp { get; set; }

        public ulong? SignalCode { get; set; }


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

        public SignalSimpleStreamTriggerEvent(
            string triggerKey,
            string sendTriggerKey,
            long timeStamp,
            ulong signals)
            : base(
                  triggerKey,
                  TriggerEventKindEnum.SimpleStreamEvent)
        {
            SendTriggerKey = sendTriggerKey;
            SendTimeStamp = timeStamp;
            SignalCode = signals;
        }
    }
}
