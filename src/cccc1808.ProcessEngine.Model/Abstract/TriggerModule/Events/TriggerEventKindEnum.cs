namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    public enum TriggerEventKindEnum
        {
            ProcessGoWaitStreamEvent,
            SimpleStreamEvent,
            RecheckProcessStatusStreamTriggerEvent,           
            DeliveryResultEvent,
            RecheckSignalFilterRootTriggerEvent,
            SignalFilterRootTriggerEvent,

            CounterEvent,
            TimerEvent,

            ProcessedOffsetEvent,
            SignalOffsetEvent,

            RemoveTriggerEvent,
        }
    
}
