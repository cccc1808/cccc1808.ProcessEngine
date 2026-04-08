namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream
{
    public interface IProcessGoWaitSpleepEvent : ITriggerEvent
    {
        IReadOnlyDictionary<string, long> ChannelsTimestampOffsets { get; }
    }
}
