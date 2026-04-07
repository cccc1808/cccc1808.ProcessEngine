using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Components
{
    public class EFOutboxMessageProxy<TId>
        : IOutboxMessageComponent<TId>
    {
        public OutboxMessageDbEntity<TId> DbEntity { get; }

        public TId Id => DbEntity.Id;
        public short Priority => DbEntity.Priority;
        public long OrderId => DbEntity.OrderId;
        public TId ProcessId => DbEntity.ProcessId;
        public bool IsActive { get => DbEntity.IsActive; set => DbEntity.IsActive = value; }
        public string Key => DbEntity.Key;
        public string IdemporencyId => DbEntity.IdemporencyId;
        public int Partition => DbEntity.Partition;
        public JsonElement Body => DbEntity.Body;
        public JsonElement Headers => DbEntity.Headers;
        public DateTimeOffset? SendDate { get => DbEntity.SendDate; set => DbEntity.SendDate = value; }        

        public EFOutboxMessageProxy(OutboxMessageDbEntity<TId> dbEntity)
        {
            DbEntity = dbEntity;
        }
    }
}
