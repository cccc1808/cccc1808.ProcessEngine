using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components
{
    public class SimpleStreamTriggerComponent
        : ISimpleStreamTriggerComponent
    {
        public string TriggerKey { get; }

        public SimpleStreamTriggerComponent(string triggerKey)
        {
            TriggerKey = triggerKey;
        }
    }
}
