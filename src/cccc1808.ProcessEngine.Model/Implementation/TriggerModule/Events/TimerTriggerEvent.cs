using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class TimerTriggerEvent<TId> :
        TriggerEvent<TId>,
        ITimerTriggerEvent<TId>
    {
        public DateTimeOffset Timer { get; set; }


        [Obsolete("Сериализатор.")]
        public TimerTriggerEvent()
        { }

        public TimerTriggerEvent(
            TId processId,
            string triggerKey,
            DateTimeOffset timer)
            : base(
                  processId,
                  triggerKey,
                  ITriggerEvent.KindEnum.TimerEvent)
        {
            Timer = timer;
        }

    }
}
