using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity
{
    public class SchemaProcessDataDbEntity<TId> :
        IId<TId>,
        IProcessLinked<TId>
    {
        public TId Id { get; set; }

        public TId ProcessId { get; set; }

        public string RootTriggerKey { get; set; }

        public string CurrentTokenId { get; set; }

        public JsonElement? CurrentTokenState { get; set; }

        public JsonElement CurrentTokenActionState { get; set; }        

        public JsonElement? ProcessState { get; set; }

        [Obsolete]
        public SchemaProcessDataDbEntity()
        {
            Id = default!;
            ProcessId = default!;
            RootTriggerKey = default!;
            CurrentTokenId = default!;
        }

        public SchemaProcessDataDbEntity(
            TId id,
            TId processId,
            string rootTriggerKey,
            string currentTokenId)
        {
            Id = id;
            ProcessId = processId;
            RootTriggerKey = rootTriggerKey;
            CurrentTokenId = currentTokenId;
            CurrentTokenActionState = JsonHelper.EmptyArray;
            CurrentTokenState = null;
            ProcessState = null;
        }
    }
}
