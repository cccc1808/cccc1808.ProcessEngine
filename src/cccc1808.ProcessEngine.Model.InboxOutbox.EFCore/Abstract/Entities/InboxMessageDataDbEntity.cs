using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities
{
    public class InboxMessageDataDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public string Key { get; set; }

        public TId StreamId { get; set; }
        public string IdemporencyId { get; set; }

        public JsonElement Body { get; set; }

        public JsonElement Headers { get; set; }
    }
}
