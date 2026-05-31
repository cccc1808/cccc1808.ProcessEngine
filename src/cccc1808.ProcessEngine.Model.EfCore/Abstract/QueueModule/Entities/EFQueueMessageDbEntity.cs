using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.QueueModule.Entities
{
    public class EFQueueMessageDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public TId QueuePartitionId { get; set; }

        public string Key { get; set; }

        public long Offset { get; set; }

        public JsonElement Headers { get; set; }

        public JsonElement Body { get; set; }

        public EFQueueMessageDbEntity(
            TId id, 
            TId queuePartitionId,
            string key,
            long offset,
            JsonElement headers,
            JsonElement body)
        {
            Id = id;
            QueuePartitionId = queuePartitionId;
            Key = key;
            Offset = offset;
            Headers = headers;
            Body = body;
        }
    }
}
