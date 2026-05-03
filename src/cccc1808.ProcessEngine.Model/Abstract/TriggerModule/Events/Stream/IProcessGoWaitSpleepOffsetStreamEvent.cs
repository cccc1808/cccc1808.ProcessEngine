namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream
{
    public interface IProcessGoWaitSpleepOffsetStreamEvent : ITriggerEvent
    {
        IReadOnlyDictionary<string, long> ProcessedChannelsOffsets { get; }
    }
}
