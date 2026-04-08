using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Services
{
    public interface IHeaderJsonSerializer
    {
        JsonElement Serialize(ICollection<HeaderDto> headers);

        HeaderDto[] Deserialize(JsonElement json);
    }
}
