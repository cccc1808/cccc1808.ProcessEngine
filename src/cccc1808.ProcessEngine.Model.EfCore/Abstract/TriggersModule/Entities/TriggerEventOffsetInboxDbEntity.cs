using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities
{
    public class TriggerEventOffsetInboxDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public string QueueName { get; set; }

        public int PartitionId { get; set; }

        public long Offset { get; set; }

        public TriggerEventOffsetInboxDbEntity(
            TId id,
            string queueName, 
            int partitionId, 
            long offset)
        {
            Id = id;
            QueueName = queueName;
            PartitionId = partitionId;
            Offset = offset;
        }
    }
}
