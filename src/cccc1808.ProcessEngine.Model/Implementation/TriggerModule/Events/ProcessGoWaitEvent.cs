using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class ProcessGoWaitEvent : TriggerEvent, IProcessGoWaitSpleepEvent
    {
        public IReadOnlyDictionary<string, long> ChannelsTimestampOffsets { get; set; }

        public ProcessGoWaitEvent(
            string triggerKey,
            IReadOnlyDictionary<string, long> channelsTimestampOffsets)
            : base(
                  triggerKey,
                  false,
                  ITriggerEvent.KindEnum.Stream_ProcessGoWaitEvent)
        {
            ChannelsTimestampOffsets = channelsTimestampOffsets;
        }
    }
}
