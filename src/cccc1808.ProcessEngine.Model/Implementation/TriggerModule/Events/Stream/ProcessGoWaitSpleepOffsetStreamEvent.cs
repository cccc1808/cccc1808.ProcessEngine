using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events.Stream
{
    /// <summary>
    /// Событие о засыпании stream процесса для StreamTrigger.
    /// </summary>
    public class ProcessGoWaitSpleepOffsetStreamEvent 
        : TriggerEvent, 
        IProcessGoWaitSpleepOffsetStreamEvent
    {
        public IReadOnlyDictionary<string, long> ProcessedChannelsOffsets { get; set; }

        [Obsolete("Сериализатор.")]
        public ProcessGoWaitSpleepOffsetStreamEvent()
        {
            ProcessedChannelsOffsets = null!;
        }

        public ProcessGoWaitSpleepOffsetStreamEvent(
            string triggerKey,
            IReadOnlyDictionary<string, long> channelsTimestampOffsets)
            : base(
                  triggerKey,
                  false,
                  ITriggerEvent.KindEnum.OffsetStream_ProcessGoWaitEvent)
        {
            ProcessedChannelsOffsets = channelsTimestampOffsets;
        }
    }
}
