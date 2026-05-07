using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events
{
    public interface IEventJsonSerializer
    {
        JsonElement Serialize(ITriggerEvent triggerEvent);

        ITriggerEvent Deserialize(JsonElement jsonElement);
    }
}
