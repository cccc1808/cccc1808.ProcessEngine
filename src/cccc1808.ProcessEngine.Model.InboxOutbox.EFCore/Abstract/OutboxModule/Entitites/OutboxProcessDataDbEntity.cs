using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites
{
    public class OutboxProcessDataDbEntity<TId>
        : IId<TId>,
        IProcessLinked<TId>
    {  
        public TId Id { get; set; } = default!;

        public TId ProcessId { get; set; } = default!;

        public TId AggregateId { get; set; } = default!;
        public AggregateClassifierDbEntity<TId> Aggregate { get; set; } = default!;

        public TId QueueId { get; set; } = default!;
        public QueueClassifierDbEntity<TId> Queue { get; set; } = default!;

        public string WakeupTriggerKey { get; set; } = default!;

        public OutboxProcessDataDbEntity() 
        { }

        public OutboxProcessDataDbEntity(TId id, TId processId, TId aggregateId, TId queueId, string wakeupTriggerKey)
        {
            Id = id;
            ProcessId = processId;
            AggregateId = aggregateId;
            QueueId = queueId;
            WakeupTriggerKey = wakeupTriggerKey;
        }
    }
}
