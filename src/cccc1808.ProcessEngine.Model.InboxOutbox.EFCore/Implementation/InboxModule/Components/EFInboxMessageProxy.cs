using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Inbox;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Components
{
    public class EFInboxMessageProxy<TId>
        : IInboxMessageComponent<TId>
    {
        public InboxMessageDbEntity<TId> DbEntity { get; }

        public TId Id => DbEntity.Id;
        public short Priority => DbEntity.Priority;
        public long OrderId => DbEntity.OrderId;
        public TId ProcessId => DbEntity.ProcessId;
        public bool IsActive { get => DbEntity.IsActive; set => DbEntity.IsActive = value; }
        public string Key => DbEntity.Key;
        public int Partition => DbEntity.Partition;
        public string IdemporencyId => DbEntity.IdemporencyId;
        public JsonElement Body => DbEntity.Body;
        public JsonElement Headers => DbEntity.Headers;

        public EFInboxMessageProxy(
            InboxMessageDbEntity<TId> dbEntity)
        {
            DbEntity = dbEntity;
        }
    }
}
