using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities
{
    public class InboxStreamDataDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }
        // public TimerProcessDbEntity<TId> Stream { get; set; }

        public TId AggregateId { get; set; }
        public AggregateClassifierDbEntity<TId> Aggregate { get; set; }

        public TId QueueId { get; set; }
        public QueueClassifierDbEntity<TId> Queue { get; set; }
    }
}
