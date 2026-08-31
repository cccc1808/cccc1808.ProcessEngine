using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service.Serializers
{
    public interface ISchemaSerializer
    {
        JsonElement Serialize(ProcessSchemaDto schema);

        ProcessSchemaDto Deserialize(JsonElement json);
    }
}
