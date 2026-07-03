using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events
{
    public class EventJsonSerializer<TId> : IEventJsonSerializer
    {
        private readonly ITriggerSetter<TId> _setter;

        public EventJsonSerializer(ITriggerSetter<TId> setter)
        {
            _setter = setter;
        }

        public ITriggerEvent Deserialize(JsonElement jsonElement)
        {
            var commonEvent = jsonElement.Deserialize<TriggerEvent>()!;
            return _setter.OneOfTriggerEventSetter.OneOfKind(
                commonEvent.Kind,
                (commonEvent, jsonElement),
                removeTriggerEventHandler: static (p) => p.jsonElement.Deserialize<RemoveTriggerEvent>(),
                counterTriggerEventHandler: static (p) => (ITriggerEvent)p.jsonElement.Deserialize<CounterTriggerEvent>()!,
                timerTriggerEventHandler: static (p) => p.jsonElement.Deserialize<TimerTriggerEvent>(),
                signalSimpleStreamTriggerEventHandler: static (p) => p.jsonElement.Deserialize<SignalSimpleStreamTriggerEvent>(),
                processGoWaitStreamTriggerEventHandler: static (p) => p.jsonElement.Deserialize<ProcessGoWaitStreamTriggerEvent>(),
                ignoreCodeSimpleStreamTriggerEventHandler: static (p) => p.jsonElement.,
                recheckIgnoreRootTriggerEventHandler: static (p) => ,
                processedOffsetTriggerEventHandler: static  (p) => p.jsonElement.Deserialize<ProcessedOffsetTriggerEvent>(),
                signalOffsetTriggerEventHandler: static (p) => p.jsonElement.Deserialize<SignalOffsetTriggerEvent>(),
                recheckProcessStatusStreamTriggerEventHandler: static (p) => p.jsonElement.Deserialize<RecheckProcessStatusStreamTriggerEvent>(),                               
                deliveryResultEventHandler: static (p) => p.jsonElement.Deserialize<DeliveryResultEvent>()
                )!;
        }

        public JsonElement Serialize(ITriggerEvent triggerEvent)
        {
            return JsonHelper.ToJsonElement(triggerEvent);
        }
    }
}
