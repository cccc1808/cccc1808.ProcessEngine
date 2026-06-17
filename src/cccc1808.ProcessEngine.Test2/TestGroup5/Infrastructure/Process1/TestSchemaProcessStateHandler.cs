using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process1
{
    internal class TestSchemaProcessStateHandler : ISchemaProcessStateHandler<Guid>
    {
        public bool IsTokenSupport(string tokenId)
        {
            return true;
        }

        public object? DeserializeProcessState(JsonElement jsonState)
        {
            throw new NotImplementedException();
        }

        public object? DeserializeTokenState(string currentTokenId, JsonElement jsonState)
        {
            throw new NotImplementedException();
        }        

        public JsonElement? SerializeProcessState(IProcessContainer<Guid> process, object state)
        {
            throw new NotImplementedException();
        }

        public JsonElement? SerializeTokenState(IProcessContainer<Guid> process, object state)
        {
            throw new NotImplementedException();
        }
    }
}
