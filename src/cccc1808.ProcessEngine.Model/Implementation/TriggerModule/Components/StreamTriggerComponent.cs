using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components
{
    public class StreamTriggerComponent
        : IStreamTriggerComponent
    {
        public string[] TriggersKeys { get; }

        public StreamTriggerComponent(string[] triggerKeys)
        {
            TriggersKeys = triggerKeys;
        }
    }
}
