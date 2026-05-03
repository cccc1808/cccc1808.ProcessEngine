using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components
{
    public static class DefaultTriggerComponent
    {
        public class SimpleStreamDto<TId> 
            : ITriggerComponent<TId>.ISimpleStreamDto
        {
            public bool StreamsProcessIsWaiting { get; set; }

            public long NewSignalCounter { get; set; }

            public SimpleStreamDto(
                bool streamsProcessIsWaiting,
                long newSignalCounter)
            {
                StreamsProcessIsWaiting = streamsProcessIsWaiting;
                NewSignalCounter = newSignalCounter;
            }
        }

        public class OffsetStreamDto<TId> 
            : ITriggerComponent<TId>.IOffsetStreamDto
        {
            public bool StreamsProcessIsWaiting { get; set; }

            public IDictionary<string, ITriggerComponent<TId>.IOffsetStreamDto.IEntryDto> ChannelsOffsets { get; }

            public OffsetStreamDto(
                bool streamsProcessIsWaiting, 
                IDictionary<string, ITriggerComponent<TId>.IOffsetStreamDto.IEntryDto> channelsOffsets)
            {
                StreamsProcessIsWaiting = streamsProcessIsWaiting;
                ChannelsOffsets = channelsOffsets;
            }            

            public class EntryDto : ITriggerComponent<TId>.IOffsetStreamDto.IEntryDto
            {
                public long LastOffset { get; set; }

                public long ProcessedOffset { get; set; }

                public EntryDto(long lastOffset, long processedOffset)
                {
                    LastOffset = lastOffset;
                    ProcessedOffset = processedOffset;
                }                
            }
        }
    }
}
