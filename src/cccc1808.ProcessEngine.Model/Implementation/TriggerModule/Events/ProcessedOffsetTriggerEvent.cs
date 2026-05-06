using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class ProcessedOffsetTriggerEvent<TId> :
        TriggerEvent<TId>,
        IProcessedOffsetTriggerEvent<TId>
    {
        public long ProcessedOffset { get; set; }

        [Obsolete("Сериализатор.")]
        public ProcessedOffsetTriggerEvent()
        { }

        public ProcessedOffsetTriggerEvent(
            TId processId,
            string triggerKey,
            long processedOffset)
            : base(
                  processId,
                  triggerKey,
                  ITriggerEvent.KindEnum.ProcessedOffsetEvent)
        {
            ProcessedOffset = processedOffset;
        }
    }
}
