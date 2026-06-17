using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process2
{
    internal class TestSchemaProcessStateHandler2 : ISchemaProcessStateHandler<Guid>
    {
        public bool IsTokenSupport(string tokenId)
        {
            return tokenId == "1";
        }

        public JsonElement? SerializeTokenState(IProcessContainer<Guid> process, object state)
        {
            return JsonHelper.ToJsonElement(state);
        }

        public JsonElement? SerializeProcessState(IProcessContainer<Guid> process, object state)
        {
            throw new NotImplementedException();
        }

        public object? DeserializeTokenState(string tokenId, JsonElement jsonState)
        {
            return tokenId switch 
            {
                "1" => jsonState.Deserialize<TestSchemaProcessHandler2.RpcTokenState>(),

                _ => throw new NotImplementedException(tokenId)
            };
        }

        public object? DeserializeProcessState(JsonElement jsonState)
        {
            throw new NotImplementedException();
        }        
    }
}
