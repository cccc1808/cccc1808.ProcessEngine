using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components
{
    public class TriggerComponent<TId> : ITriggerComponent<TId>
    {
        public string Key { get; }

        public ITriggerComponent.TriggerKind Kind { get; }

        public TId ProcessId { get; }

        public bool IsActivated { get; set; }

        public bool IsCompleted { get; set; }

        public DateTimeOffset TimerDate { get; set; }

        public string HandlerKey { get; }

        public DateTimeOffset SelectLockTimeout { get; set; }

        public object State { get; } 
            = null!;

        public bool NeedUpdate { get; set; }

        public bool NeedRemove { get; set; }

        public TriggerComponent(
            string key,
            ITriggerComponent.TriggerKind kind,
            TId processId,
            bool isActivated,
            bool isCompleted,
            DateTimeOffset timerDate,
            string handlerKey,
            DateTimeOffset selectLockTimeout,
            object? state)
        {
            Key = key;
            Kind = kind;
            ProcessId = processId;
            IsActivated = isActivated;
            IsCompleted = isCompleted;
            TimerDate = timerDate;
            HandlerKey = handlerKey;
            SelectLockTimeout = selectLockTimeout;
            State = kind switch
            {
                ITriggerComponent.TriggerKind.Counter => (ITriggerComponent.ICounterDto)state,
                ITriggerComponent.TriggerKind.Timer => null!,
                ITriggerComponent.TriggerKind.SimpleStream => (ITriggerComponent.ISimpleStreamDto)state,
                ITriggerComponent.TriggerKind.SimpleStreamRoot => (ITriggerComponent.ISimpleStreamDto)state,
                ITriggerComponent.TriggerKind.OffsetStream => (ITriggerComponent.IOffsetStreamDto)state,

                _ => throw new NotImplementedException($"{kind}")
            };
        }

        public class CounterDto : ITriggerComponent.ICounterDto
        {
            public long Counter { get; set; }

            public CounterDto(
                long counter) 
            {
                Counter = counter; 
            } 
        }

        public class SimpleStreamDto : ITriggerComponent.ISimpleStreamDto
        {
            public bool StreamsProcessIsWaiting { get; set; }

            public long NewSignalCounter { get; set; }

            public bool IsRootTrigger { get; set; }

            public SimpleStreamDto(
                bool streamsProcessIsWaiting,
                long newSignalCounter,
                bool isRootTrigger)
            {
                StreamsProcessIsWaiting = streamsProcessIsWaiting;
                NewSignalCounter = newSignalCounter;
                IsRootTrigger = isRootTrigger;
            }
        }

        public class OffsetStreamDto : ITriggerComponent.IOffsetStreamDto
        {
            public bool StreamsProcessIsWaiting { get; set; }

            public long ProcessedOffset { get; set; }

            public long LastOffset { get; set; }            

            public OffsetStreamDto(
                bool streamsProcessIsWaiting,
                long processedOffset,
                long lastOffset)
            {
                StreamsProcessIsWaiting = streamsProcessIsWaiting;
                ProcessedOffset = processedOffset;
                LastOffset = lastOffset;                
            }
        }
    }
}
