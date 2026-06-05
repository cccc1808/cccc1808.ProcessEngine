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
        public string TriggerEventQueue { get; }

        public string[] TriggersKeys { get; set; }        

        public StreamTriggerComponent(
            string triggerEventQueue,
            string[] triggerKeys)
        {
            TriggerEventQueue = triggerEventQueue;
            TriggersKeys = triggerKeys;
        }
    }
}
