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
    public class OutboxProcessDataDbEntity<TId>
        : IId<TId>,
        IProcessLinkedDbEntity<TId>
    {
        public TId Id { get; set; }

        public TId ProcessId { get; set; }

        public TId AggregateId { get; set; }
        public AggregateClassifierDbEntity<TId> Aggregate { get; set; }

        public TId QueueId { get; set; }
        public QueueClassifierDbEntity<TId> Queue {  get; set; }
    }
}
