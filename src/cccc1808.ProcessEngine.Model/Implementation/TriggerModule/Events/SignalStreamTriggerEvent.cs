using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    /// <summary>
    /// Событие о поступлении нового сигнала в StreamTrigger.
    /// </summary>
    public class SignalStreamTriggerEvent : 
        TriggerEvent,
        ISignalStreamTriggerEvent
    {
        public string ChannelName { get; set; }

        public long ChannelTimestamp { get; set; }

        public SignalStreamTriggerEvent(
            string triggerKey,
            string channelName,
            long channelTimestamp)
            : base(
                  triggerKey, 
                  false,
                  ITriggerEvent.KindEnum.Stream_SignalEvent)
        {
            ChannelName = channelName;
            ChannelTimestamp = channelTimestamp;
        }
    }
}
