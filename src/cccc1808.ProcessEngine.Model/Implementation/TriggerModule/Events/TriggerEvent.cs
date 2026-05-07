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

        public TriggerEventKindEnum Kind { get; set; }        

        [Obsolete("Сериализатор.")]
        public TriggerEvent()
            : this(null!, default)
        {
        }

        //public TriggerEvent(
        //    TId processId,
        //    string triggerKey,
        //    bool ignoreDelay) 
        //    : this(
        //          processId,
        //          triggerKey,
        //          ignoreDelay,
        //          ITriggerEvent.KindEnum.SimpleEvent)
        //{}

        protected TriggerEvent(
            string triggerKey,
            TriggerEventKindEnum kind)
        {
            TriggerKey = triggerKey;
            Kind = kind;
        }
    }
}
