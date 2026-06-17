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
                Classes = new List<ClassDto>(),
                Relations = new Dictionary<string, List<RelationDto>>(),
            };

            var classes = new List<ClassDto>();
            ClassDto? currentClass = null;

            foreach (var elem in rows.Skip(4))
            {
                if (currentClass is null)
                {
                    if (elem.StartsWith("    class"))
                    {
                        var parts = elem.Substring("    class".Length).Split("{");
                        var name = parts[0].Trim();

                        currentClass = new ClassDto()
                        {
                            Name = name,
                            Properties = new Dictionary<string, string>(10),
                        };
                    }
                    else if (elem.Contains("--|>"))
                    {
                        var relationParts = elem.Split("--|>")
                            .Select(e => e.Trim())
                            .ToArray();

                        var relation = new RelationDto() 
                        {
                            SourceId = relationParts[0],
                            TargetId = relationParts[1],
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
                        result.Classes.Add(currentClass);
                        currentClass = null;
                    }
                    else 
                    {
                        throw new Exception($"Not support row {elem}");
                    }
                }
            }

            return result;
        }

        public class ClassDiagramDto
        {
            public List<ClassDto> Classes { get; set; }

            public Dictionary<string, List<RelationDto>> Relations { get; set; }

            public class ClassDto
            {
                public string Name { get; set; }

                public string Label { get; set; }

                public string Annotation { get; set; }

                public Dictionary<string, string> Properties { get; set; }
            }

            public class RelationDto 
            {
                public string SourceId { get; set; }

                public string TargetId { get; set; }
            }
        }
    }
}
