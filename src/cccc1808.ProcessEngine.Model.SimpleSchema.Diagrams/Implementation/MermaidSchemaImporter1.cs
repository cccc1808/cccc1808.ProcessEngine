using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Diagrams.Implementation
{
    public class MermaidSchemaImporter1
    {
        public ProcessSchemaDto Import(
            MermaidParser.ClassDiagramDto diagramDto)
        {
            static MermaidSchemaExporter1.TypeEnum ParseType(
                MermaidParser.ClassDiagramDto.ClassDto elem)
            {
                if (
                    elem.Properties.TryGetValue("Type", out var typeString)
                    && Enum.TryParse<MermaidSchemaExporter1.TypeEnum>(typeString, out var typeValue))
                {
                    return typeValue;
                }

                throw new Exception($"Не известный тип {typeString}");
            }

            static MermaidSchemaExporter1.TokenActionEnum ParseActionType(
                MermaidParser.ClassDiagramDto.ClassDto elem)
            {
                if (
                    elem.Properties.TryGetValue("ActionType", out var typeString)
                    && Enum.TryParse<MermaidSchemaExporter1.TokenActionEnum>(typeString, out var typeValue))
                {
                    return typeValue;
                }

                throw new Exception($"Не известный тип {typeString}");
            }
            static string? ValueOrNull(string value) 
            {
                return value == "null" 
                    ? null 
                    : value;
            }

            var tokenInfos = new Dictionary<string, MermaidParser.ClassDiagramDto.ClassDto>(diagramDto.Classes.Count);
            var actions = new Dictionary<string, ITokenAction>(diagramDto.Classes.Count);
            var transitionTargets = new HashSet<string>(diagramDto.Classes.Count);

            foreach (var elem in diagramDto.Classes.Values)
            {
                var type = ParseType(elem);

                switch (type)
                {
                    case MermaidSchemaExporter1.TypeEnum.End:
                        {
                            break;
                        }

                    case MermaidSchemaExporter1.TypeEnum.Token:
                        {
                            tokenInfos.Add(elem.Name, elem);

                            break;
                        }

                    case MermaidSchemaExporter1.TypeEnum.TokenAction:
                        {
                            var actionType = ParseActionType(elem);

                            var id = elem.Name.Split(MermaidSchemaExporter1.Prefix)[1];

                            var name = elem.Annotation;
                            var desription = elem.Notes.SingleOrDefault();

                            var activatedOnStart = bool.Parse(elem.Properties[nameof(ITokenAction.ActivatedOnStart)]);

                            var canRunAction = diagramDto.Relations.TryGetValue(elem.Name, out var relations)
                                ? relations
                                    .Where(e => e.Kind == MermaidParser.ClassDiagramDto.RelationDto.KindEnum.Association)
                                    .Select(e => new ITokenAction.RunActionDeclarationDto(
                                        ActivateActionId: e.TargetId.Split(MermaidSchemaExporter1.Prefix)[1],
                                        Comment: e.Label
                                        ))
                                    .ToArray()
                                : Array.Empty<ITokenAction.RunActionDeclarationDto>();

                            var transitionRelation = diagramDto.Relations.TryGetValue(elem.Name, out relations)
                                ? relations.SingleOrDefault(e => e.Kind == MermaidParser.ClassDiagramDto.RelationDto.KindEnum.Inheritance)
                                : null;

                            ITokenAction.TransitionDto? transition = null;
                            if (transitionRelation != null)
                            {
                                if (transitionRelation.TargetId == "-1")
                                {
                                    transition = ITokenAction.TransitionDto.Complete();
                                }
                                else
                                {
                                    transition = ITokenAction.TransitionDto.Target(
                                        transitionRelation.TargetId, 
                                        comment: transitionRelation.Label);
                                    transitionTargets.Add(transition.Value.TargetTokenId);
                                }                                
                            }

                            switch (actionType)
                            {
                                case MermaidSchemaExporter1.TokenActionEnum.ServiceTask:
                                    {
                                        var action = new ServiceTaskTokenAction(
                                            id,
                                            handlerKey: elem.Properties[nameof(ServiceTaskTokenAction.HandlerKey)]
                                            ) 
                                        {
                                            Name = name,
                                            Description = desription,
                                            ActivatedOnStart = activatedOnStart,
                                            Transition = transition,
                                            CanRunAction = canRunAction,
                                        };
                                        actions.Add(elem.Name, action);
                                        break;
                                    }

                                case MermaidSchemaExporter1.TokenActionEnum.Condition:
                                    {
                                        var action = new ConditionTokenAction(
                                            id,
                                            checkHandlerKey: elem.Properties[nameof(ConditionTokenAction.CheckHandlerKey)])
                                        {
                                            Name = name,
                                            Description = desription,
                                            ActivatedOnStart = activatedOnStart,
                                            ActionHandlerKey = ValueOrNull(elem.Properties[nameof(ConditionTokenAction.ActionHandlerKey)]),
                                            Transition = transition,
                                            CanRunAction = canRunAction,
                                        };
                                        actions.Add(elem.Name, action);
                                        break;
                                    }

                                case MermaidSchemaExporter1.TokenActionEnum.Timer:
                                    {
                                        var action = new TimerTokenAction(
                                            id, 
                                            TimeSpan.Parse(elem.Properties[nameof(TimerTokenAction.Duration)]))
                                        {
                                            Name = elem.Annotation,
                                            Description = elem.Notes.SingleOrDefault(),
                                            ActivatedOnStart = activatedOnStart,
                                            HandlerKey = ValueOrNull(elem.Properties[nameof(TimerTokenAction.HandlerKey)]),
                                            Transition = transition,
                                            CanRunAction = canRunAction,
                                        };
                                        actions.Add(elem.Name, action);
                                        break;
                                    }

                                default: 
                                    throw new NotImplementedException(actionType.ToString());
                            }

                            break;
                        }

                    default:
                        throw new NotImplementedException(type.ToString());
                }
            }

            var tokens = new List<TokenDto>(tokenInfos.Count);
            foreach (var elem in tokenInfos)
            {
                var token = new TokenDto(
                    elem.Key,
                    actions: diagramDto
                        .Relations[elem.Key]
                        .Select(e => actions[e.TargetId])
                        .ToArray()
                    )
                { 
                    Name = elem.Value.Annotation,
                    Description = elem.Value.Notes.SingleOrDefault(),
                };
                tokens.Add(token);
            }

            var startToken = tokens.Single(e => !transitionTargets.Contains(e.Id));

            return new ProcessSchemaDto(
                startToken.Id,
                tokens
                )
            { 
                Description = diagramDto.Notes.SingleOrDefault(),
            };
        }
        
    }
}
