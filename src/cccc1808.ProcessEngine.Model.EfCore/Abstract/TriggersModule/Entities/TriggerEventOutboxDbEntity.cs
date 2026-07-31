using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities
{
    public class TriggerEventOutboxDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public long Timestamp { get; set; }

        public short BatchOrderId { get; set; }

        public JsonElement Data { get; set; }


        public TriggerEventOutboxDbEntity(
            TId id, 
            long timestamp,
            short batchOrderId,
            JsonElement data)
        {
            Id = id;
            Timestamp = timestamp;
            BatchOrderId = batchOrderId;
            Data = data;            
        }


        public readonly record struct EventDto(
            string EventQueue,
            TId ProcessId,
            JsonElement Event);
    }
}
