using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;

using FoggyBalrog.MermaidDotNet;
using FoggyBalrog.MermaidDotNet.ClassDiagram.Model;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Diagrams.Implementation
{
    public class MermaidSchemaExporter1
    {
        public string Export(
            ProcessSchemaDto schema,
            string title)
        {
            var builder = Mermaid.ClassDiagram(title);
           
            builder.AddClass("-1", out var endNode, annotation: "End");

            builder.AddProperty(
               endNode,
               "Type",
               TypeEnum.End.ToString());

            var tokensNodes = new Dictionary<string, Class>(schema.Tokens.Count);

            foreach (var elem in schema.Tokens.Values)
            {
                builder.AddClass(
                    name: elem.Id, 
                    out var tokenNode, 
                    annotation: elem.Name);

                builder.AddProperty(
                    tokenNode, 
                    "Type",
                    TypeEnum.Token.ToString());

                tokensNodes.Add(elem.Id, tokenNode);
            }

            foreach (var elem in schema.Tokens.Values)
            {
                foreach (var elem2 in elem.Actions)
                {
                    builder.AddClass(
                        name: $"{elem.Id}.{elem2.Id}",
                        out var actionNode,
                        annotation: elem2.Name);

                    builder.AddProperty(
                        actionNode,
                        "Type",
                        TypeEnum.TokenAction.ToString());

                    ITokenAction.TransitionDto? transition = null;
                    switch (elem2)
                    {
                        case ServiceTaskTokenAction serviceTaskTokenAction:
                            {
                                builder.AddProperty(
                                    actionNode,
                                    "ActionType",
                                    TokenActionEnum.ServiceTask.ToString());

                                builder.AddProperty(
                                    actionNode,
                                    nameof(serviceTaskTokenAction.HandlerKey),
                                    serviceTaskTokenAction.HandlerKey);

                                transition = serviceTaskTokenAction.Transition;

                                break;
                            }

                        case TimerTokenAction timerTokenAction:
                            {
                                builder.AddProperty(
                                    actionNode,
                                    "ActionType",
                                    TokenActionEnum.Timer.ToString());

                                builder.AddProperty(
                                    actionNode,
                                    nameof(timerTokenAction.Duration),
                                    timerTokenAction.Duration.ToString());

                                builder.AddProperty(
                                    actionNode,
                                    nameof(timerTokenAction.HandlerKey),
                                    timerTokenAction.HandlerKey ?? "null");

                                transition = timerTokenAction.Transition;

                                break;
                            }

                        case ConditionTokenAction conditionTokenAction:
                            {
                                builder.AddProperty(
                                    actionNode,
                                    "ActionType",
                                    TokenActionEnum.Condition.ToString());

                                builder.AddProperty(
                                    actionNode,
                                    nameof(conditionTokenAction.CheckHandlerKey),
                                    conditionTokenAction.CheckHandlerKey);

                                builder.AddProperty(
                                    actionNode,
                                    nameof(conditionTokenAction.ActionHandlerKey),
                                    conditionTokenAction.ActionHandlerKey ?? "null");

                                transition = conditionTokenAction.Transition;

                                break;
                            }

                        default: 
                            throw new NotImplementedException(elem2.GetType().FullName);
                    }

                    builder.AddRelationship(
                        from: tokensNodes[elem.Id],
                        to: actionNode,
                        fromRelationshipType: RelationshipType.Unspecified,
                        toRelationshipType: RelationshipType.Inheritance);

                    if (transition.HasValue)
                    {
                        if (transition.Value.TargetTokenId is not null)
                        {
                            builder.AddRelationship(
                                from: actionNode,
                                to: tokensNodes[transition.Value.TargetTokenId],
                                fromRelationshipType: RelationshipType.Unspecified,
                                toRelationshipType: RelationshipType.Inheritance);
                        }
                        else 
                        {
                            builder.AddRelationship(
                                from: actionNode,
                                to: endNode,
                                fromRelationshipType: RelationshipType.Unspecified,
                                toRelationshipType: RelationshipType.Inheritance);
                        }
                    }
                }
            }

            return builder.Build();
        }

        public enum TypeEnum 
        {
            End,
            Token,
            TokenAction
        }

        public enum TokenActionEnum 
        {
            ServiceTask,
            Timer,
            Condition
        }
    }
}
