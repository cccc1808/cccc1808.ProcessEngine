using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events.Stream;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events
{
    public class EventJsonSerializer : IEventJsonSerializer
    {
        public ITriggerEvent Deserialize(JsonElement jsonElement)
        {
            var commonEvent = JsonSerializer.Deserialize<TriggerEvent>(jsonElement);

            return commonEvent.Kind switch
            {
                ITriggerEvent.KindEnum.WakeupSignalEvent => commonEvent,

                ITriggerEvent.KindEnum.SimpleStream_SignalEvent => JsonSerializer.Deserialize<SignalSimpleStreamTriggerEvent>(jsonElement)!,
                ITriggerEvent.KindEnum.SimpleStream_ProcessGoWaitEvent => JsonSerializer.Deserialize<ProcessGoWaitSpleepSimpleStreamEvent>(jsonElement)!,

                ITriggerEvent.KindEnum.OffsetStream_SignalEvent => JsonSerializer.Deserialize<SignalOffsetStreamTriggerEvent>(jsonElement)!,
                ITriggerEvent.KindEnum.OffsetStream_ProcessGoWaitEvent => JsonSerializer.Deserialize<ProcessGoWaitSpleepOffsetStreamEvent>(jsonElement)!,

                _ => throw new Exception($"[Bug] неподдерживаемое событие триггера {commonEvent.Kind}.")
            };
        }

        public JsonElement Serialize(ITriggerEvent triggerEvent)
        {
            using var doc = JsonSerializer.SerializeToDocument(
                triggerEvent, 
                triggerEvent.GetType()
                );
            return doc.RootElement.Clone();
        }
    }
}
