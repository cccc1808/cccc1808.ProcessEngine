using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static cccc1808.ProcessEngine.Model.SimpleSchema.Diagrams.Implementation.MermaidParser.ClassDiagramDto;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Diagrams.Implementation
{
    public class MermaidParser
    {
        public ClassDiagramDto ParseClassDiagramm(string mermaid)
        {
            var rows = mermaid
                .Split(Environment.NewLine)
                .ToArray();

            if (rows[3] != "classDiagram")
            {
                throw new ArgumentException("classDiagram");
            }

            var result = new ClassDiagramDto() 
            {
                Classes = new Dictionary<string, ClassDto>(),
                Relations = new Dictionary<string, List<RelationDto>>(),
                Notes = new List<string>(1),
            };

            var notes = new Dictionary<string, List<string>>();

            ClassDto? currentClass = null;

            foreach (var elem in rows.Skip(4))
            {
                if (currentClass is null)
                {
                    if (
                        string.IsNullOrWhiteSpace(elem)
                        || elem.StartsWith("    cssClass")
                        || elem.StartsWith("    classDef")
                        )
                    {
                        // Игнорируем элементы стиля
                        continue;
                    }
                    if (elem.StartsWith("    class"))
                    {
                        var parts = elem.Substring("    class".Length).Split("{");
                        var name = parts[0].Trim().TrimStart(MermaidSchemaExporter1.Prefix);

                        currentClass = new ClassDto()
                        {
                            Name = name,
                            Properties = new Dictionary<string, string>(10),
                            Notes = new List<string>(0),
                            Annotation = null!,
                            Label = null!,
                        };
                    }                    
                    else if (elem.StartsWith("    note for"))
                    {
                        var parts = elem.Substring("    note for".Length)
                            .Split('"', StringSplitOptions.TrimEntries)
                            .ToArray();

                        if (!notes.TryGetValue(parts[0], out var collection))
                        {
                            collection = new List<string>(1);
                            notes.Add(parts[0].TrimStart(MermaidSchemaExporter1.Prefix), collection);
                        }

                        collection.Add(
                            parts[1].Replace(MermaidSchemaExporter1.NoteNewLine, Environment.NewLine));
                    }
                    else if (elem.StartsWith("    note"))
                    {
                        var parts = elem.Substring("    note".Length)
                            .Split('"', StringSplitOptions.TrimEntries)
                            .ToArray();

                        result.Notes.Add(
                            parts[1].Replace(MermaidSchemaExporter1.NoteNewLine, Environment.NewLine));
                    }
                    else if (elem.Contains("--|>"))
                    {
                        var relationParts = elem.Split("--|>")
                            .Select(e => e.Trim())
                            .ToArray();

                        var relation = new RelationDto()
                        {
                            SourceId = relationParts[0].TrimStart(MermaidSchemaExporter1.Prefix),
                            TargetId = relationParts[1].TrimStart(MermaidSchemaExporter1.Prefix),
                            Kind = RelationDto.KindEnum.Inheritance,
                        };

                        if (!result.Relations.TryGetValue(relation.SourceId, out var relations))
                        {
                            relations = new List<RelationDto>();
                            result.Relations.Add(relation.SourceId, relations);
                        }

                        relations.Add(relation);
                    }
                    else if (elem.Contains("-->"))
                    {
                        var relationParts = elem.Split("-->")
                            .Select(e => e.Trim())
                            .ToArray();

                        var relation = new RelationDto()
                        {
                            SourceId = relationParts[0].TrimStart(MermaidSchemaExporter1.Prefix),
                            TargetId = relationParts[1].TrimStart(MermaidSchemaExporter1.Prefix),
                            Kind = RelationDto.KindEnum.Association,
                        };

                        if (!result.Relations.TryGetValue(relation.SourceId, out var relations))
                        {
                            relations = new List<RelationDto>();
                            result.Relations.Add(relation.SourceId, relations);
                        }

                        relations.Add(relation);
                    }
                    else
                    {
                        throw new Exception($"Not support row {elem}");
                    }
                }
                else
                {
                    if (elem.StartsWith("        <<"))
                    {
                        var annotation = elem.Substring("        <<".Length).Split(">>")[0];
                        currentClass.Annotation = annotation;
                    }
                    else if (elem.StartsWith("        +"))
                    {
                        var propertiesParts = elem.Substring("        +".Length).Split(" ");
                        currentClass.Properties.Add(propertiesParts[0], propertiesParts[1]);
                    }
                    else if (elem.StartsWith("    }"))
                    {
                        result.Classes.Add(currentClass.Name, currentClass);
                        currentClass = null;
                    }
                    else 
                    {
                        throw new Exception($"Not support row {elem}");
                    }
                }
            }

            foreach (var elem in notes)
            {
                result.Classes[elem.Key].Notes = elem.Value;
            }

            return result;
        }

        public class ClassDiagramDto
        {
            public required Dictionary<string, ClassDto> Classes { get; set; }

            public required Dictionary<string, List<RelationDto>> Relations { get; set; }

            public required List<string> Notes { get; set; }

            public class ClassDto
            {
                public required string Name { get; set; }                

                public required string Label { get; set; }

                public required string Annotation { get; set; }

                public required Dictionary<string, string> Properties { get; set; }

                public required List<string> Notes { get; set; }

                public override string ToString()
                {
                    return Name;
                }
            }

            public class RelationDto 
            {
                public required string SourceId { get; set; }

                public required string TargetId { get; set; }

                public required KindEnum Kind { get; set; }


                public enum KindEnum 
                {
                    Inheritance,
                    Association
                }

                public override string ToString()
                {
                    return $"{SourceId} -> {TargetId}";
                }
            }
        }
    }
}
