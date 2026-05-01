using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Components
{
    public class StreamTriggerComponent 
        : IStreamTriggerComponent
    {
        public string TriggerKey { get; }

        public IDictionary<string, long> ProcessedChannels { get; } 
            = new Dictionary<string, long>(5);

        public StreamTriggerComponent(string triggerKey)
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
