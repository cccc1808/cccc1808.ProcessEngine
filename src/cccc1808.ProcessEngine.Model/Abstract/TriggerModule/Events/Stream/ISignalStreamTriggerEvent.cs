namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events.Stream
{
    public interface ISignalStreamTriggerEvent : ITriggerEvent
    {
        string StreamKey { get; }

        long StreamTimestamp { get; }
    }
}
