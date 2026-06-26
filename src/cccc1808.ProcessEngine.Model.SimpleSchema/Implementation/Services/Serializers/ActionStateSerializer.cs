using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Component.ActionComponent;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service.Serializers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services.Serializers
{
    public class ActionStateSerializer 
        : IActionStateSerializer
    {
        public JsonElement Serialize(IEnumerable<ITokenActionStateComponent> data)
        {
            var container = data
                .Select(
                    e => new ActionStateContainer() 
                    {
                        Id = e.Id,
                        Kind = e switch 
                        {
                            ServiceTaskActionState => SchemaSerializer.KindEnum.ServiceTask,
                            TimerActionStateComponent => SchemaSerializer.KindEnum.Timer,
                            ConditionActionStateComponent => SchemaSerializer.KindEnum.Condition,

                            _ => throw new NotImplementedException(e.GetType().FullName)
                        },
                        Data = JsonHelper.ToJsonElement(e)
                    }
                    )
                .ToArray();

            return JsonHelper.ToJsonElement(container);
        }

        public ITokenActionStateComponent[] Deserialize(JsonElement json)
        {
            var container = json.Deserialize<ActionStateContainer[]>()!;

            var result = container
                .Select(
                    e => e.Kind switch
                    {
                        SchemaSerializer.KindEnum.ServiceTask => (ITokenActionStateComponent)e.Data.Deserialize<ServiceTaskActionState>()!,
                        SchemaSerializer.KindEnum.Timer => e.Data.Deserialize<TimerActionStateComponent>(),
                        SchemaSerializer.KindEnum.Condition => e.Data.Deserialize<ConditionActionStateComponent>(),

                        _ => throw new NotImplementedException(e.Kind.ToString())
                    }
                    )
                .ToArray();

            return result;
        }        

        public class ActionStateContainer
        {
            public string Id { get; set; } = default!;

            public SchemaSerializer.KindEnum Kind { get; set; }

            public JsonElement Data { get; set; }
        }
    }
}
