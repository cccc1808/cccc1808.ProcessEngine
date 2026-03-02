using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.MessageStream.EFCore.Abstract;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities
{
    public class OutboxMessageDbEntity<TId>
        : IMessageDbEntity<TId>,
        IProcessLinkedDbEntity<TId>
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
    }
}
