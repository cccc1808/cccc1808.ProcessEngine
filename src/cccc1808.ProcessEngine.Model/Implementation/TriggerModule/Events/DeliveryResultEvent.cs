using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class DeliveryResultEvent :
        TriggerEvent,
        IDeliveryResultEvent
    {
        public long Timestamp { get; set; }

        [Obsolete("Сериализатор.")]
        public DeliveryResultEvent()
        { }

        public DeliveryResultEvent(
            string triggerKey,
            long timestamp)
            : base(
                  triggerKey,
                  TriggerEventKindEnum.DeliveryResultEvent)
        {
            Timestamp = timestamp;
        }
    }
}
