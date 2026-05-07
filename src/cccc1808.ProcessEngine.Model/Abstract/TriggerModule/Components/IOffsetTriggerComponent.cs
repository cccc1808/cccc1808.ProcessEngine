using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components
{
    public interface IOffsetTriggerComponent
    {
        string TriggerEventQueue { get; }

        IDictionary<string, long> ProcessedOffsets { get; }

        long UpdateMaxTimestamp(
            string triggerKey,
            long timestamp);
    }
}
