using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities
{
    internal class OutboxStreamDataDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }
        // public TimerProcessDbEntity<TId> Stream { get; set; }

        public string AggregateId { get; set; }

        public TId QueueId { get; set; }
        public QueueClassifierDbEntity<TId> Queue {  get; set; }
    }
}
