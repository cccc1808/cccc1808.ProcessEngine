using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services
{
    public class SchemaSerializer 
        : ISchemaSerializer
    {
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
                                        Kind = e switch 
                                        { 
                                            ServiceTaskTokenAction => KindEnum.ServiceTask, 
                                            TimerTokenAction => KindEnum.Timer,
                                            ConditionTokenAction => KindEnum.Condition,

                                            _ => throw new NotImplementedException(e.GetType().FullName)
                                        },
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
                                    e => e.Kind switch 
                                    {
                                        KindEnum.ServiceTask => (ITokenAction)e.Data.Deserialize<ServiceTaskTokenAction>()!,
                                        KindEnum.Timer => e.Data.Deserialize<TimerTokenAction>()!,
                                        KindEnum.Condition => e.Data.Deserialize<ConditionTokenAction>()!,

                                        _ => throw new NotImplementedException(e.Kind.ToString())
                                    }
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

            public required KindEnum Kind { get; set; }

            public required JsonElement Data { get; set; }
        }

        public enum KindEnum
        {
            ServiceTask,
            Condition,
            Timer,
        }
    }
}
