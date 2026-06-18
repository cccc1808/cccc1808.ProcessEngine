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
        public static string NoteNewLine { get; set; }
            = @"<br>";

        public static char Prefix { get; set; }
            = '_';

        public string Export(
            ProcessSchemaDto schema,
            ExportOptions options)
        {
            const string tokenCssClass = "TokenStyle";
            const string actionCssClass = "ActionStyle";

            var builder = Mermaid.ClassDiagram(options.Title);

            builder.AddClass("-1", out var endNode, annotation: "End");

            if (schema.Description is not null)
            {
                builder.AddNote(
                    schema.Description.Replace(Environment.NewLine, NoteNewLine));
            }

            builder.AddProperty(
               endNode,
               "Type",
               TypeEnum.End.ToString());

            var tokensNodes = new Dictionary<string, Class>(schema.Tokens.Count);

            foreach (var elem in schema.Tokens.Values)
            {
                builder.AddClass(
                    name: $"{Prefix}{elem.Id}", 
                    out var tokenNode, 
                    annotation: elem.Name);

                builder.StyleWithCssClass(tokenCssClass, tokenNode);

                if (elem.Description is not null)
                {
                    builder.AddNote(
                        elem.Description.Replace(Environment.NewLine, NoteNewLine), 
                        tokenNode);
                }                

                builder.AddProperty(
                    tokenNode, 
                    "Type",
                    TypeEnum.Token.ToString());

                tokensNodes.Add(elem.Id, tokenNode);
            }

            var actionsNodes = new Dictionary<string, Class>(schema.Tokens.Count);

            foreach (var elem in schema.Tokens.Values)
            {
                foreach (var elem2 in elem.Actions)
                {
                    builder.AddClass(
                        name: $"{Prefix}{elem.Id}{Prefix}{elem2.Id}",
                        out var actionNode,
                        annotation: elem2.Name);

                    builder.StyleWithCssClass(actionCssClass, actionNode);

                    actionsNodes.Add($"{elem.Id}.{elem2.Id}", actionNode);

                    if (elem2.Description is not null)
                    {
                        builder.AddNote(
                            elem2.Description.Replace(Environment.NewLine, NoteNewLine), 
                            actionNode);
                    }

                    builder.AddProperty(
                        actionNode,
                        "Type",
                        TypeEnum.TokenAction.ToString());

                    builder.AddProperty(
                        actionNode,
                        nameof(elem2.ActivatedOnStart),
                        elem2.ActivatedOnStart.ToString());

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
                                toRelationshipType: RelationshipType.Inheritance,
                                label: "transition");
                        }
                        else 
                        {
                            builder.AddRelationship(
                                from: actionNode,
                                to: endNode,
                                fromRelationshipType: RelationshipType.Unspecified,
                                toRelationshipType: RelationshipType.Inheritance,
                                label: "Transition");
                        }
                    }
                }

                if (options.withCanRun)
                {
                    foreach (var elem2 in elem.Actions)
                    {
                        foreach (var elem3 in elem2.CanRunAction)
                        {
                            builder.AddRelationship(
                                from: actionsNodes[$"{elem.Id}.{elem2.Id}"],
                                to: actionsNodes[$"{elem.Id}.{elem3}"],
                                fromRelationshipType: RelationshipType.Unspecified,
                                toRelationshipType: RelationshipType.Association,
                                label: "Run action");
                        }
                    }
                }
            }

            return $@"{builder.Build()}

    classDef {tokenCssClass} fill:#5BFF90;
    classDef {actionCssClass} fill:#F197FF;";
        }

        public readonly record struct ExportOptions(
            string Title,
            bool withCanRun);

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
