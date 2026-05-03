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

        /// <summary>
        /// TODO: можно переделать на число для экономии.
        /// </summary>
        public string HandlerKey { get; set; }

        public ITriggerComponent<TId>.TriggerKind Kind { get; set; }

        public short Priority { get; set; }

        public bool IsActivated { get; set; }

        public bool IsCompleted { get; set; }

        public TId ProcessId { get; set; }

        public int? Counter { get; set; }

        public JsonElement? StreamData
        {
            get
            {
                switch (Kind)
                {
                    case ITriggerComponent<TId>.TriggerKind.SimpleStream: 
                        {
                            using (var document = JsonSerializer.SerializeToDocument(SimpleStreamState))
                            {
                                return document.RootElement.Clone();
                            }
                        }

                    case ITriggerComponent<TId>.TriggerKind.OffsetStream: 
                        {
                            using (var document = JsonSerializer.SerializeToDocument(OffsetStreamState))
                            {
                                return document.RootElement.Clone();
                            }
                        }

                    case ITriggerComponent<TId>.TriggerKind.Counter:
                    case ITriggerComponent<TId>.TriggerKind.Timer: 
                        return null;

                    default: throw new NotImplementedException($"{Kind}.");
                }
            }
            set
            {
                switch (Kind)
                {
                    case ITriggerComponent<TId>.TriggerKind.SimpleStream:
                        {
                            SimpleStreamState = JsonSerializer.Deserialize<SimpleStreamDto>(value.Value);
                            break;
                        }

                    case ITriggerComponent<TId>.TriggerKind.OffsetStream:
                        {
                            OffsetStreamState = JsonSerializer.Deserialize<OffsetStreamDto>(value.Value);
                            break;
                        }

                    case ITriggerComponent<TId>.TriggerKind.Counter:
                    case ITriggerComponent<TId>.TriggerKind.Timer:
                        break;

                    default: throw new NotImplementedException($"{Kind}.");
                }
            }
        }

        public SimpleStreamDto? SimpleStreamState { get; private set; }

        public OffsetStreamDto? OffsetStreamState { get; private set; }

        [Obsolete("For entity framework")]
        public TriggerDbEntity() 
        {
            Id = default!;
            Key = default!;
            ProcessId = default!;
            HandlerKey = default!;
            SimpleStreamState = null;
            OffsetStreamState = null!;
        }

        public TriggerDbEntity(
            TId id, 
            string key, 
            DateTimeOffset selectLockTimeout, 
            DateTimeOffset timerDate,
            string handlerKey,
            ITriggerComponent<TId>.TriggerKind kind,
            short priority, 
            bool isActivated, 
            bool isCompleted,
            TId processId, 
            int? counter,
            (SimpleStreamDto? simpleStream, OffsetStreamDto? offsettampStream)? streamState
            )
        {
            Id = id;
            Key = key;
            SelectLockTimeout = selectLockTimeout;
            TimerDate = timerDate;
            HandlerKey = handlerKey;
            Kind = kind;
            Priority = priority;
            IsActivated = isActivated;
            IsCompleted = isCompleted;
            ProcessId = processId;
            Counter = counter;

            switch (kind)
            {
                case ITriggerComponent<TId>.TriggerKind.SimpleStream:
                    {
                        SimpleStreamState = streamState.Value.simpleStream;
                        break;
                    }

                case ITriggerComponent<TId>.TriggerKind.OffsetStream:
                    {
                        OffsetStreamState = streamState.Value.offsettampStream;
                        break;
                    }

                case ITriggerComponent<TId>.TriggerKind.Timer:
                case ITriggerComponent<TId>.TriggerKind.Counter:
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
