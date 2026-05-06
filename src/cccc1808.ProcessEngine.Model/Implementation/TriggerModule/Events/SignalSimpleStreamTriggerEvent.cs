using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class SignalSimpleStreamTriggerEvent<TId> :
        TriggerEvent<TId>,
        ISignalSimpleStreamTriggerEvent<TId>
    {
        [Obsolete("Сериализатор.")]
        public SignalSimpleStreamTriggerEvent()
        { }

        public SignalSimpleStreamTriggerEvent(
            TId processId,
            string triggerKey)
            : base(
                  processId,
                  triggerKey,
                  ITriggerEvent.KindEnum.SimpleStreamEvent)
        {
        }
    }
}
