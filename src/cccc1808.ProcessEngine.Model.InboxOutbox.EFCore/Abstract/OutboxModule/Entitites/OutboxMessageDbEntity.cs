using System.Text.Json;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.MessageStreamModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites
{
    public class OutboxMessageDbEntity<TId>
        : IMessageDbEntity<TId>,
        IProcessLinked<TId>
    {
        #region IMessageDbEntity

        public TId Id { get; set; } = default!;

        public int Partition { get; set; }

        public short Priority { get; set; }

        public long OrderId { get; set; }

        public TId ProcessId { get; set; } = default!;

        public bool IsActive { get; set; }

        #endregion

        public string Key { get; set; } = default!;

        public string IdemporencyId { get; set; } = default!;

        public JsonElement Body { get; set; }

        public JsonElement Headers { get; set; }

        public DateTimeOffset? SendDate { get; set; }

        public OutboxMessageDbEntity() { }

        public OutboxMessageDbEntity(TId id, int partition, short priority, long orderId, TId processId, bool isActive, string key, string idemporencyId, JsonElement body, JsonElement headers, DateTimeOffset? sendDate)
        {
            Id = id;
            Partition = partition;
            Priority = priority;
            OrderId = orderId;
            ProcessId = processId;
            IsActive = isActive;
            Key = key;
            IdemporencyId = idemporencyId;
            Body = body;
            Headers = headers;
            SendDate = sendDate;
        }
    }
}
