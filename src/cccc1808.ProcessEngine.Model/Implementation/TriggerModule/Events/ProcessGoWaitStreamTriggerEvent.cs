using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class ProcessGoWaitStreamTriggerEvent<TId> :
        TriggerEvent<TId>,
        IProcessGoWaitStreamTriggerEvent<TId>
    {
        [Obsolete("Сериализатор.")]
        public ProcessGoWaitStreamTriggerEvent()
        { }

        public ProcessGoWaitStreamTriggerEvent(
            TId processId,
            string triggerKey)
            : base(
                  processId,
                  triggerKey,
                  ITriggerEvent.KindEnum.ProcessGoWaitStreamEvent)
        {
        }
    }
}
