using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities
{
    public class TriggerDbEntity<TId>
        : IId<TId>, 
        IProcessLinked<TId>
    {
        public TId Id { get; set; }

        public string Key { get; set; }

        /// <summary>
        /// Используется в том числе для индекса, позволяет меньше конкурировать нодам.
        /// Дополняет updatelock.
        /// </summary>
        public DateTimeOffset SelectLockTimeout { get; set; }

        public DateTimeOffset TimerDate { get; set; }

        public bool IsRangeHandler { get; set; }

        /// <summary>
        /// TODO: можно переделать на число для экономии.
        /// </summary>
        public string HandlerKey { get; set; }

        public ITriggerComponent.TriggerKind Kind { get; set; }

        public short Priority { get; set; }

        public bool IsActivated { get; set; }

        public bool IsCompleted { get; set; }

        public TId ProcessId { get; set; }

        public bool? StreamProcessIsWaiting { get; set; }

        public long? SignalCounter1 { get; set; }

        public long? SignalCounter2 { get; set; }


        [Obsolete("For entity framework")]
        public TriggerDbEntity() 
        {
            Id = default!;
            Key = default!;
            ProcessId = default!;
            HandlerKey = default!;
        }

        public TriggerDbEntity(
            TId id,
            string key,
            DateTimeOffset selectLockTimeout,
            DateTimeOffset timerDate,
            bool isRangeHandler,
            string handlerKey,
            ITriggerComponent.TriggerKind kind,
            short priority,
            bool isActivated,
            bool isCompleted,
            TId processId,
            bool? streamProcessIsWaiting,
            long? signalCounter1,
            long? signalCounter2)
        {
            Id = id;
            Key = key;
            SelectLockTimeout = selectLockTimeout;
            TimerDate = timerDate;
            IsRangeHandler = isRangeHandler;
            HandlerKey = handlerKey;
            Kind = kind;
            Priority = priority;
            IsActivated = isActivated;
            IsCompleted = isCompleted;
            ProcessId = processId;

            switch (kind)
            {
                case ITriggerComponent.TriggerKind.Counter:
                    {
                        SignalCounter1 = signalCounter1.Value;
                        break;
                    }

                case ITriggerComponent.TriggerKind.SimpleStream:
                    {
                        StreamProcessIsWaiting = streamProcessIsWaiting.Value;
                        SignalCounter1 = signalCounter1.Value;
                        break;
                    }

                case ITriggerComponent.TriggerKind.OffsetStream:
                    {
                        StreamProcessIsWaiting = streamProcessIsWaiting.Value;
                        SignalCounter1 = signalCounter1.Value;
                        SignalCounter2 = signalCounter2.Value;
                        break;
                    }

                case ITriggerComponent.TriggerKind.Timer:                
                    break;

                default: throw new NotImplementedException($"{Kind}.");
            }            
        }

        public class SimpleStreamDto 
        {
            public bool StreamsProcessIsWaiting { get; set; }

            public long NewSignalCounter { get; set; }

            [Obsolete("Serialization.")]
            public SimpleStreamDto() { }

            public SimpleStreamDto(
                bool streamsProcessIsWaiting,
                long newSignalCounter)
            {
                StreamsProcessIsWaiting = streamsProcessIsWaiting;
                NewSignalCounter = newSignalCounter;
            }
        }

        public class OffsetStreamDto
        {
            public bool StreamsProcessIsWaiting { get; set; }

            public Dictionary<string, OffsetEntry> ChannelsOffsets { get; set; }

            [Obsolete("Serialization.")]
            public OffsetStreamDto()
            {
                ChannelsOffsets = null!;
            }

            public OffsetStreamDto(
                bool streamsProcessIsWaiting,
                Dictionary<string, OffsetEntry> channelsOffsets)
            {
                StreamsProcessIsWaiting = streamsProcessIsWaiting;
                ChannelsOffsets = channelsOffsets;
            }

            public class OffsetEntry 
            {               

                public long LastOffset { get; set; }

                public long ProcessedOffset { get; set; }

                [Obsolete("Serialization.")]
                public OffsetEntry()
                {}

                public OffsetEntry(long lastOffset, long processedOffset)
                {
                    LastOffset = lastOffset;
                    ProcessedOffset = processedOffset;
                }
            }
        }
    }
}
