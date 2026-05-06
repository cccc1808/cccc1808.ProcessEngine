using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class SignalOffsetTriggerEvent<TId> :
        TriggerEvent<TId>,
        ISignalOffsetTriggerEvent<TId>
    {
        public long UpdateOffset { get; set; }

        [Obsolete("Сериализатор.")]
        public SignalOffsetTriggerEvent() 
        { }

        public SignalOffsetTriggerEvent(
            TId processId,
            string triggerKey,
            long updateOffset)
            : base(
                  processId,
                  triggerKey,
                  ITriggerEvent.KindEnum.SignalOffsetEvent)
        {
            UpdateOffset = updateOffset;
        }
    }
}
