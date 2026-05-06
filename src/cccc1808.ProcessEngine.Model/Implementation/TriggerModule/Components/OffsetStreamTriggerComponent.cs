using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components
{
    public class OffsetStreamTriggerComponent 
        : IOffsetTriggerComponent
    {
        public IDictionary<string, long> ProcessedOffsets { get; } 
            = new Dictionary<string, long>(5);

        public OffsetStreamTriggerComponent()
        {}

        public void UpdateMaxTimestamp(string triggerKey, long offset)
        {
            if (ProcessedOffsets.TryGetValue(triggerKey, out var exsist))
            {
                ProcessedOffsets[triggerKey] = Math.Max(offset, exsist);
            }
            else 
            {
                ProcessedOffsets.Add(triggerKey, offset);
            }
        }
    }
}
