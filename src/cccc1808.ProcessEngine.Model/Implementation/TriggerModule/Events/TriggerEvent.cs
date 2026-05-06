using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class TriggerEvent<TId> : ITriggerEvent<TId>
    {
        public TId ProcessId { get; set; }

        public string TriggerKey { get; set; }

        public ITriggerEvent.KindEnum Kind { get; set; }        

        [Obsolete("Сериализатор.")]
        public TriggerEvent()
        {
            ProcessId = default!;
            TriggerKey = null!;
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
            TId processId,
            string triggerKey,
            ITriggerEvent.KindEnum kind)
        {
            ProcessId = processId;
            TriggerKey = triggerKey;
            Kind = kind;
        }
    }
}
