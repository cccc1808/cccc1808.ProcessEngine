using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class SignalStreamTriggerEvent : 
        TriggerEvent,
        ISignalStreamTriggerEvent
    {
        public string StreamKey { get; set; }

        public long StreamTimestamp { get; set; }

        public SignalStreamTriggerEvent(
            string triggerKey,
            ITriggerEvent.KindEnum kind,
            string channelKey,
            long signalTimestamp)
            : base(
                  triggerKey, 
                  false,
                  kind)
        {
            StreamKey = channelKey;
            StreamTimestamp = signalTimestamp;
        }
    }
}
