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

        public ITriggerEvent.KindEnum Kind { get; set; }

        [Obsolete("Сериализатор.")]
        public TriggerEvent() 
        {
            TriggerKey = null!;
            IgnoreDelay = false;
        }

        public TriggerEvent(
            string triggerKey,
            bool ignoreDelay) 
            : this(
                  triggerKey,
                  ignoreDelay,
                  ITriggerEvent.KindEnum.WakeupSignalEvent)
        {}

        protected TriggerEvent(
            string triggerKey,
            bool ignoreDelay,
            ITriggerEvent.KindEnum kind)
        {
            TriggerKey = triggerKey;
            IgnoreDelay = ignoreDelay;
            Kind = kind;
        }
    }
}
