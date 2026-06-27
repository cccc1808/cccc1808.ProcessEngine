using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Handlers
{
    public interface ISchemaProcessStateHandler<TId>
    {
        bool IsTokenSupport(string tokenId);

        JsonElement? SerializeProcessState(IProcessContainer<TId> process, object state);

        object DeserializeProcessState(JsonElement jsonState);

        JsonElement? SerializeTokenState(IProcessContainer<TId> process, object state);

        object DeserializeTokenState(string currentTokenId, JsonElement jsonState);        
    }
}
