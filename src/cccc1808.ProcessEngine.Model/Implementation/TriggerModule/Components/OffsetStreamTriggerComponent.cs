using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components
{
    public class OffsetStreamTriggerComponent 
        : IOffsetStreamTriggerComponent
    {
        public string TriggerKey { get; }

        public IDictionary<string, long> ProcessedChannels { get; } 
            = new Dictionary<string, long>(5);

        public OffsetStreamTriggerComponent(string triggerKey)
        {
            TriggerKey = triggerKey;
        }

        public void UpdateMaxTimestamp(string channelName, long timestamp)
        {
            if (ProcessedChannels.TryGetValue(channelName, out var exsist))
            {
                ProcessedChannels[channelName] = Math.Max(timestamp, exsist);
            }
            else 
            {
                ProcessedChannels.Add(channelName, timestamp);
            }
        }
    }
}
