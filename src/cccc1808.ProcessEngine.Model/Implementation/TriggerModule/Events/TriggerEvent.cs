using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class TriggerEvent : ITriggerEvent
    {
        public string TriggerKey { get; set; }

        public bool IgnoreDelay { get; set; }

        [Obsolete("Сериализатор.")]
        public TriggerEvent() 
        {
            TriggerKey = null!;
            IgnoreDelay = false;
        }

        public TriggerEvent(
            string triggerKey,
            bool ignoreDelay)
        {
            TriggerKey = triggerKey;
            IgnoreDelay = ignoreDelay;
        }
    }
}
