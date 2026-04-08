using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Services;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.CommonModule.Services
{
    public class HeaderJsonSerializer : IHeaderJsonSerializer
    {
        public HeaderDto[] Deserialize(JsonElement json)
        {
            return JsonSerializer.Deserialize<HeaderDto[]>(json);
        }

        public JsonElement Serialize(ICollection<HeaderDto> headers)
        {
            using var document = JsonSerializer.SerializeToDocument(headers);
            return document.RootElement.Clone();
        }
    }
}
