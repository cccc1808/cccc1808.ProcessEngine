using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components
{
    public interface IMessageComponent<TId>
    {
        public TId Id { get; }

        public short Priority { get; }

        public long OrderId { get; }

        public TId ProcessId { get; }

        public bool IsActive { get; set; }


        public string Key { get; }

        public int Partition { get; }

        public string IdemporencyId { get; }

        public JsonElement Body { get; }

        public JsonElement Headers { get; }
    }
}
