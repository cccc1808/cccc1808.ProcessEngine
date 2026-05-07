using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class CounterTriggerEvent :
        TriggerEvent,
        ICounterTriggerEvent
    {
        public bool Reset { get; set; }

        public int Value { get; set; }

        [Obsolete("Сериализатор.")]
        public CounterTriggerEvent()
        { }

        public CounterTriggerEvent(
            string triggerKey,            
            int value,
            bool reset = false)
            : base(
                  triggerKey,
                  TriggerEventKindEnum.CounterEvent)
        {
            Reset = reset;
            Value = value;
        }
    }
}
