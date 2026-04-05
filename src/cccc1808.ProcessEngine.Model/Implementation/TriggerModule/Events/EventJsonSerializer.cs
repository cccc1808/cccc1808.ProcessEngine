using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class EventJsonSerializer : IEventJsonSerializer
    {
        public ITriggerEvent Deserialize(JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<TriggerEvent>(jsonElement);
        }

        public JsonElement Serialize(ITriggerEvent triggerEvent)
        {
            using var doc = JsonSerializer.SerializeToDocument(triggerEvent);
            return doc.RootElement.Clone();
        }
    }
}
