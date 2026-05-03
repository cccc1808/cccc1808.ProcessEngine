namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream
{
    public interface ISignalOffsetStreamTriggerEvent : ITriggerEvent
    {
        string ChannelName { get; }

        long ChannelOffset { get; }
    }
}
