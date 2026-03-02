using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.Entities.Classifiers;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities
{
    public class InboxProcessDataDbEntity<TId>
        : IId<TId>,
        IProcessLinkedDbEntity<TId>
    {
        public TId Id { get; set; } = default!;

        public TId ProcessId { get; set; } = default!;
        // public ProcessDbEntity<TId> Process { get; set; }

        public TId AggregateId { get; set; } = default!;
        public AggregateClassifierDbEntity<TId> Aggregate { get; set; } = default!;

        public TId QueueId { get; set; } = default!;
        public QueueClassifierDbEntity<TId> Queue { get; set; } = default!;
    }
}
