using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Component.ActionComponent;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service.Serializers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services.Serializers
{
    public class ActionStateSerializer 
        : IActionStateSerializer
    {
        private readonly ISchemaProcessActionSetter _schemaProcessActionSetter;

        public ActionStateSerializer(
            ISchemaProcessActionSetter schemaProcessActionSetter)
        {
            _schemaProcessActionSetter = schemaProcessActionSetter;
        }

        public JsonElement Serialize(IEnumerable<ITokenActionStateComponent> data)
        {
            var container = data
                .Select(
                    e => new ActionStateContainer() 
                    {
                        Id = e.Id,
                        Kind = _schemaProcessActionSetter.CommonSetter.GetKind(e),
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
                    e => _schemaProcessActionSetter.CommonSetter.OneOfKind(
                        e,
                        e.Kind,
                        serviceTaskHandler: static (e) => (ITokenActionStateComponent)e.Data.Deserialize<ServiceTaskActionState>()!,
                        conditionHandler: static (e) => e.Data.Deserialize<ConditionActionStateComponent>(),
                        timerHandler: static (e) => e.Data.Deserialize<TimerActionStateComponent>())
                    )
                .ToArray();

            return result;
        }        

        public class ActionStateContainer
        {
            public string Id { get; set; } = default!;

            public TokenActionKindEnum Kind { get; set; }

            public JsonElement Data { get; set; }
        }
    }
}
