using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.Diagrams.Implementation;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process1;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process2;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process3;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process4;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup5
{
    public class DiagrammaTest
    {
        [Fact]
        public void Test1()
        {
            var serializer = new SchemaSerializer();
            var exporter = new MermaidSchemaExporter1();
            var parser = new MermaidParser();
            var import = new MermaidSchemaImporter1();

            var diagramm = exporter.Export(
                TestSchemaProcessHandler.Schema,
                new MermaidSchemaExporter1.ExportOptions(
                    TestSchemaProcessHandler.ProcessType.ToString(), 
                    withCanRun: true));

            var model = parser.ParseClassDiagramm(diagramm);
            var importedSchema = import.Import(model);

            serializer.Serialize(TestSchemaProcessHandler.Schema).GetRawText()
                .ShouldBeEquivalentTo(serializer.Serialize(importedSchema).GetRawText());
        }

        [Fact]
        public void Test2()
        {
            var serializer = new SchemaSerializer();
            var exporter = new MermaidSchemaExporter1();
            var parser = new MermaidParser();
            var import = new MermaidSchemaImporter1();

            var diagramm = exporter.Export(
                TestSchemaProcessHandler2.Schema,
                new MermaidSchemaExporter1.ExportOptions(
                    TestSchemaProcessHandler2.ProcessType.ToString(),
                    withCanRun: true));

            var model = parser.ParseClassDiagramm(diagramm);
            var importedSchema = import.Import(model);

            serializer.Serialize(TestSchemaProcessHandler2.Schema).GetRawText()
                .ShouldBeEquivalentTo(serializer.Serialize(importedSchema).GetRawText());
        }

        [Fact]
        public void Test3()
        {
            var serializer = new SchemaSerializer();
            var exporter = new MermaidSchemaExporter1();
            var parser = new MermaidParser();
            var import = new MermaidSchemaImporter1();

            var diagramm = exporter.Export(
                TestSchemaProcessHandler3.Schema,
                new MermaidSchemaExporter1.ExportOptions(
                    TestSchemaProcessHandler3.ProcessType.ToString(), 
                    withCanRun: true));

            var model = parser.ParseClassDiagramm(diagramm);
            var importedSchema = import.Import(model);

            serializer.Serialize(TestSchemaProcessHandler3.Schema).GetRawText()
                .ShouldBeEquivalentTo(serializer.Serialize(importedSchema).GetRawText());
        }

        [Fact]
        public void Test4()
        {
            var serializer = new SchemaSerializer();
            var exporter = new MermaidSchemaExporter1();
            var parser = new MermaidParser();
            var import = new MermaidSchemaImporter1();

            var diagramm = exporter.Export(
                TestSchemaProcessHandler4.Schema,
                new MermaidSchemaExporter1.ExportOptions(
                    TestSchemaProcessHandler4.ProcessType.ToString(),
                    withCanRun: true));

            var model = parser.ParseClassDiagramm(diagramm);
            var importedSchema = import.Import(model);

            serializer.Serialize(TestSchemaProcessHandler4.Schema).GetRawText()
                .ShouldBeEquivalentTo(serializer.Serialize(importedSchema).GetRawText());
        }
    }
}
