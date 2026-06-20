using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.Diagrams.Implementation;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process1;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process2;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process3;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process4;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process5;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup5
{
    public class DiagrammaTest
    {
        [Fact]
        public void Test1()
        {
            TestDiagram(TestSchemaProcessHandler.Schema);
        }

        [Fact]
        public void Test2()
        {
            TestDiagram(TestSchemaProcessHandler2.Schema);
        }

        [Fact]
        public void Test3()
        {
            TestDiagram(TestSchemaProcessHandler3.Schema);
        }

        [Fact]
        public void Test4()
        {
            TestDiagram(TestSchemaProcessHandler4.Schema);
        }

        [Fact]
        public void Test5()
        {
            TestDiagram(TestSchemaProcessHandler51.Schema);
            TestDiagram(TestSchemaProcessHandler52.Schema);
        }

        private void TestDiagram(
            ProcessSchemaDto schema)
        {
            var serializer = new SchemaSerializer();
            var exporter = new MermaidSchemaExporter1();
            var parser = new MermaidParser();
            var import = new MermaidSchemaImporter1();

            var diagramm = exporter.Export(
                schema,
                new MermaidSchemaExporter1.ExportOptions(
                    "T",
                    withCanRun: true));

            var model = parser.ParseClassDiagramm(diagramm);
            var importedSchema = import.Import(model);

            serializer.Serialize(schema).GetRawText()
                .ShouldBeEquivalentTo(serializer.Serialize(importedSchema).GetRawText());
        }
    }
}
