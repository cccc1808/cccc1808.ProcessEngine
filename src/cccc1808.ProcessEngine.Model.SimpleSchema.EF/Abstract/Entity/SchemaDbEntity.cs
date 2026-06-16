using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity
{
    public class SchemaDbEntity<TId> 
        : IId<TId>
    {
        public TId Id { get; set; }

        public long ProcessTypeId { get; set; }

        public int ProcessVersion { get; set; }

        public JsonElement Schema { get; set; }

        public string HandlerKey { get; set; }


        public SchemaDbEntity(
            TId id, 
            long processTypeId, 
            int processVersion,
            JsonElement schema,
            string handlerKey)
        {
            Id = id;
            ProcessTypeId = processTypeId;
            ProcessVersion = processVersion;
            Schema = schema;
            HandlerKey = handlerKey;
        }
    }
}
