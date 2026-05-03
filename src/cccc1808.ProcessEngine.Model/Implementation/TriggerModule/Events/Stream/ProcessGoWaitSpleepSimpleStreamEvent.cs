using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events.Stream
{
    public class ProcessGoWaitSpleepSimpleStreamEvent
        : TriggerEvent,
        IProcessGoWaitSpleepSimpleStreamEvent
    {
        [Obsolete("Сериализатор.")]
        public ProcessGoWaitSpleepSimpleStreamEvent()
        {
        }

        public ProcessGoWaitSpleepSimpleStreamEvent(string triggerKey)
            : base(
                  triggerKey,
                  ignoreDelay: false,
                  ITriggerEvent.KindEnum.SimpleStream_ProcessGoWaitEvent)
        { }
    }
}
