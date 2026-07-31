using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service.Serializers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services.Serializers
{
    public class SchemaSerializer 
        : ISchemaSerializer
    {
        private readonly ISchemaProcessActionSetter _schemaProcessActionSetter;

        public SchemaSerializer(
            ISchemaProcessActionSetter schemaProcessActionSetter)
        {
            _schemaProcessActionSetter = schemaProcessActionSetter;
        }

        public JsonElement Serialize(ProcessSchemaDto schema)
        {
            var container = new ProcessSchemaDtoContainer() 
            {
                StartTokenId = schema.StartTokenId,
                Tokens = schema.Tokens
                    .Values
                    .Select(
                        e => new TokenContainer() 
                        {
                            Id = e.Id,
                            Name = e.Name,
                            Actions = e.Actions
                                .Select(
                                    e => new ActionContainer() 
                                    {
                                        Id = e.Id,
                                        Name = e.Name,
                                        Kind = _schemaProcessActionSetter.CommonSetter.GetKind(e),
                                        Data = JsonHelper.ToJsonElement(e)
                                    })
                                .ToArray(),
                        })
                    .ToArray(),
            };

            return JsonHelper.ToJsonElement(container);
        }

        public ProcessSchemaDto Deserialize(JsonElement json)
        {
            var container = json.Deserialize<ProcessSchemaDtoContainer>();

            var result = new ProcessSchemaDto(
                container.StartTokenId,
                container.Tokens
                    .Select(
                        e => new TokenDto(
                            e.Id,
                            e.Actions
                                .Select(
                                    e => _schemaProcessActionSetter.CommonSetter.OneOfKind(
                                        e,
                                        e.Kind,
                                        serviceTaskHandler: static (e) => (ITokenAction)e.Data.Deserialize<ServiceTaskTokenAction>()!,
                                        conditionHandler: static (e) => e.Data.Deserialize<ConditionTokenAction>()!,
                                        timerHandler: static (e) => e.Data.Deserialize<TimerTokenAction>()!
                                        )
                                    )
                                .ToArray()
                        )
                        {
                            Name = e.Name
                        }
                        )
                    .ToArray()
                );          

            return result;
        }       


        public class ProcessSchemaDtoContainer
        {
            public string StartTokenId { get; set; } = default!;

            /// <summary>
            /// Токены схемы процесса.
            /// </summary>
            public TokenContainer[] Tokens { get; set; } = default!;
        }

        public class TokenContainer 
        {
            public required string Id { get; set; } = default!;

            public required string? Name { get; set; } = default!;

            public required ActionContainer[] Actions { get; set; } = default!;
        }

        public class ActionContainer
        {
            public required string Id { get; set; } = default!;

            public required string? Name { get; set; } = default!;

            public required TokenActionKindEnum Kind { get; set; }

            public required JsonElement Data { get; set; }
        }
    }
}
