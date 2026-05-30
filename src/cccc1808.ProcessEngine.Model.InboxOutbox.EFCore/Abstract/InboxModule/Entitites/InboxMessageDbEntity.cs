using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.MessageStreamModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites
{
    public class InboxMessageDbEntity<TId>
        : IMessageDbEntity<TId>,
        IProcessLinked<TId>
    {        
        #region IMessageDbEntity

        public TId Id { get; set; } = default!;

        public short Priority { get; set; }

        public long OrderId { get; set; }

        public TId ProcessId { get; set; } = default!;

        public bool IsActive { get; set; }

        #endregion

        public string Key { get; set; } = default!;

        public int Partition { get; set; }

        public string IdempotencyId { get; set; } = default!;

        public JsonElement Body { get; set; }

        public JsonElement Headers { get; set; }

        public InboxMessageDbEntity() { }

        public InboxMessageDbEntity(
            TId id,
            short priority, 
            long orderId, 
            TId processId,
            bool isActive,
            string key, 
            int partition, 
            string idemporencyId, 
            JsonElement body, 
            JsonElement headers)
        {
            Id = id;
            Priority = priority;
            OrderId = orderId;
            ProcessId = processId;
            IsActive = isActive;
            Key = key;
            Partition = partition;
            IdempotencyId = idemporencyId;
            Body = body;
            Headers = headers;
        }
    }
}
