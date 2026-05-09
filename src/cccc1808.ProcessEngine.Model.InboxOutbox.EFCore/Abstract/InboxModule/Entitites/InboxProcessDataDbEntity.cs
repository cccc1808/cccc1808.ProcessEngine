using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites
{
    public class InboxProcessDataDbEntity<TId>
        : IId<TId>,
        IProcessLinked<TId>
    {
        public TId Id { get; set; } = default!;

        public TId ProcessId { get; set; } = default!;
        // public ProcessDbEntity<TId> Process { get; set; }

        public TId AggregateId { get; set; } = default!;
        public AggregateClassifierDbEntity<TId> Aggregate { get; set; } = default!;

        public TId QueueId { get; set; } = default!;
        public QueueClassifierDbEntity<TId> Queue { get; set; } = default!;

        public long ProcessedOffset { get; set; } = default!;

        public string WakeupTriggerKey { get; set; } = default!;


        public InboxProcessDataDbEntity(
            TId id, 
            TId processId, 
            TId aggregateId,
            TId queueId,
            long processedOffset,
            string wakeupTriggerKey)
        {
            Id = id;
            ProcessId = processId;
            AggregateId = aggregateId;
            QueueId = queueId;
            ProcessedOffset = processedOffset;
            WakeupTriggerKey = wakeupTriggerKey;
        }
    }
}
