using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Svc2CodeConverter
{
    /// <summary>
    /// Так я и не смог добраться до количества элементов в генерируемом свойстве List или array, и также проставить комменты на хотя-бы главные методы
    /// Таких дебрей не ожидал даже через XmlSchemaImporter и XmlCodeExporter, что уже говорить про WsdlImport с его замутами
    /// </summary>
    public class XsdCodeGen0
    {
        private static XmlSchemas AddFiles2Schemas(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Directory {path} not exists.");
                return null;
            }

            var xsdsFilesNames = new DirectoryInfo(path).GetFiles("*.xsd", SearchOption.AllDirectories);

            var xsdSchemas = new XmlSchemas();

            foreach (var xsdPath in xsdsFilesNames)
            {
                using (var stream = new FileStream(xsdPath.FullName, FileMode.Open, FileAccess.Read))
                {
                    Console.WriteLine($" Read File {xsdPath.FullName}");
                    var xsd = XmlSchema.Read(stream, null);
                    xsdSchemas.Add(xsd);
                    Console.WriteLine($" IsCompiled? {xsd.IsCompiled}");
                    Console.WriteLine();
                }
            }

            return xsdSchemas;
        }

        private static CodeCompileUnit CreateXsdMappings(XmlSchemas xsdSchemas, CodeCompileUnit codeCompileUnit)
        {
            var xmlTypeMapping = new List<XmlTypeMapping>();
            var schemaImporter = new XmlSchemaImporter(xsdSchemas);

            foreach (var xsd in xsdSchemas.Where(s => s.TargetNamespace.Equals("http://dom.gosuslugi.ru/schema/integration/bills/")))
            {
                //Library.SerializeXml(xsd);

                //var schemaTypes = xsd.SchemaTypes.Values.Cast<XmlSchemaType>()
                //    .Select(t => t);

                /*var elements = xsd.Elements.Values.Cast<XmlSchemaElement>()
                    .Select(t => t);*/

                xmlTypeMapping.AddRange(
                    xsd.SchemaTypes.Values.Cast<XmlSchemaType>()
                        .Where(t => t.Name.Equals("PaymentDocumentExportType"))//для теста
                        .Select(t => schemaImporter.ImportSchemaType(t.QualifiedName)));

                /*xmlTypeMapping.AddRange(
                    xsd.Elements.Values.Cast<XmlSchemaElement>()
                        .Select(t => schemaImporter.ImportTypeMapping(t.QualifiedName)));*/

                foreach (var stype in xsd.SchemaTypes.Values.Cast<XmlSchemaType>()
                        //.Where(t => t.Name.Equals("PaymentDocumentExportType"))//для теста
                        .Select(t => schemaImporter.ImportSchemaType(t.QualifiedName)).ToList())
                {
                    GenerateDom(stype, codeCompileUnit);
                }
                
            }

            return codeCompileUnit;
        }

        private static CodeCompileUnit GenerateDom(/*IList<*/XmlTypeMapping/*>*/ xmlTypeMapping, CodeCompileUnit codeCompileUnit)
        {
            //var codeNamespace = new CodeNamespace("Generated_" + Guid.NewGuid().ToString().Split('-').First());
            var codeNamespace = new CodeNamespace(xmlTypeMapping.XsdTypeName);

            var codeExporter = new XmlCodeExporter(codeNamespace/*, codeCompileUnit, CodeGenerationOptions.GenerateOrder*/);

            //foreach (var xmlTypeMap in xmlTypeMapping)
            {
                codeExporter.ExportTypeMapping(xmlTypeMapping);
            }

            CodeGenerator.ValidateIdentifiers(codeNamespace);
            codeCompileUnit.Namespaces.Add(codeNamespace);

            return codeCompileUnit;
        }

        public static CodeCompileUnit GenerateCodeFromXsdFromPath(string xsdPathName)
        {
            var xsdSchemas = AddFiles2Schemas(xsdPathName);
            if (xsdSchemas == null)
            {
                return null;
            }

            xsdSchemas.Compile(null, true);

            var codeCompileUnit = new CodeCompileUnit
            {
                UserData = { { "ModuleName", "Gis" } }
            };

            var newCodeCompileUnit = CreateXsdMappings(xsdSchemas, codeCompileUnit);
            
            return newCodeCompileUnit;
        }
    }
}
