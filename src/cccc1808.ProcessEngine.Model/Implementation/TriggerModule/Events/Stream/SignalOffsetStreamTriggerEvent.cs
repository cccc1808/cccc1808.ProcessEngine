using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events.Stream
{
    /// <summary>
    /// Событие о поступлении нового сигнала в StreamTrigger.
    /// </summary>
    public class SignalOffsetStreamTriggerEvent : 
        TriggerEvent,
        ISignalOffsetStreamTriggerEvent
    {
        public string ChannelName { get; set; }

        public long ChannelOffset { get; set; }

        [Obsolete("Сериализатор.")]
        public SignalOffsetStreamTriggerEvent()
        {
            ChannelName = null!;
        }

        public SignalOffsetStreamTriggerEvent(
            string triggerKey,
            string channelName,
            long channelTimestamp)
            : base(
                  triggerKey, 
                  false,
                  ITriggerEvent.KindEnum.OffsetStream_SignalEvent)
        {
            ChannelName = channelName;
            ChannelOffset = channelTimestamp;
        }
    }
}
