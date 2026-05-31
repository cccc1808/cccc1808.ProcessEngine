using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.QueueModule.Entities
{
    public class EFQueuePartitionDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public string TopicName { get; set; }

        public int PartitionId { get; set; }   
        
        public DateTimeOffset ProcessDate { get; set; }

        public EFQueuePartitionDbEntity(
            TId id, 
            string topicName, 
            int partitionId,
            DateTimeOffset processDate)
        {
            Id = id;
            TopicName = topicName;
            PartitionId = partitionId;
            ProcessDate = processDate;
        }
    }
}
