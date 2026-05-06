using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class CounterTriggerEvent<TId> :
        TriggerEvent<TId>,
        ICounterTriggerEvent<TId>
    {
        public bool Reset { get; set; }

        public int Value { get; set; }

        [Obsolete("Сериализатор.")]
        public CounterTriggerEvent()
        { }

        public CounterTriggerEvent(
            TId processId,
            string triggerKey,            
            int value,
            bool reset = false)
            : base(
                  processId,
                  triggerKey,
                  ITriggerEvent.KindEnum.CounterEvent)
        {
            Reset = reset;
            Value = value;
        }
    }
}
