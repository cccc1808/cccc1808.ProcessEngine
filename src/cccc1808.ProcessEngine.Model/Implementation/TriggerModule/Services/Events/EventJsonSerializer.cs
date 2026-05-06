using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events
{
    public class EventJsonSerializer<TId> : IEventJsonSerializer<TId>
    {
        private readonly ITriggerSetter<TId> _setter;

        public EventJsonSerializer(ITriggerSetter<TId> setter)
        {
            _setter = setter;
        }

        public ITriggerEvent<TId> Deserialize(JsonElement jsonElement)
        {
            var commonEvent = jsonElement.Deserialize<TriggerEvent<TId>>()!;
            return _setter.OneOfEventKind(
                commonEvent.Kind,
                (commonEvent, jsonElement),
                counterTriggerEventHandler: static (p) => (ITriggerEvent<TId>)p.jsonElement.Deserialize<CounterTriggerEvent<TId>>(),
                timerTriggerEventHandler: static (p) => p.jsonElement.Deserialize<TimerTriggerEvent<TId>>(),
                signalSimpleStreamTriggerEventHandler: static (p) => p.jsonElement.Deserialize<SignalSimpleStreamTriggerEvent<TId>>(),
                processGoWaitStreamTriggerEventHandler: static (p) => p.jsonElement.Deserialize<ProcessGoWaitStreamTriggerEvent<TId>>(),
                processedOffsetTriggerEventHandler: static  (p) => p.jsonElement.Deserialize<ProcessedOffsetTriggerEvent<TId>>(),
                signalOffsetTriggerEventHandler: static (p) => p.jsonElement.Deserialize<SignalOffsetTriggerEvent<TId>>()
                )!;
        }

        public JsonElement Serialize(ITriggerEvent<TId> triggerEvent)
        {
            using var doc = JsonSerializer.SerializeToDocument(
                triggerEvent, 
                triggerEvent.GetType()
                );
            return doc.RootElement.Clone();
        }
    }
}
