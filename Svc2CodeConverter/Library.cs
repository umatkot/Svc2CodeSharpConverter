using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using FluentNHibernate.Utils;
using Newtonsoft.Json;
using NHibernate.Util;
using Svc2CodeConverter.Extensions;

namespace Svc2CodeConverter
{
    public class Library : LibraryExtender
    {
        private static readonly string SitLogin = ConfigurationManager.AppSettings["basic_user"] ?? "sit";
        private static readonly string SitPassword = ConfigurationManager.AppSettings["basic_password"] ?? "rZ_GG72XS^Vf55ZW";
        private static readonly string CurrentPlatform = ConfigurationManager.AppSettings["current_platform"];
        private static readonly string ServicePointAddress = ConfigurationManager.AppSettings["servicepoint_" + CurrentPlatform];

        public static CodeCompileUnit[] LoadSvcData(string[] serviceEndpoints, string globalNamespaceName)
        {
            var concurrentDic = new ConcurrentDictionary<string, CodeCompileUnit>();
            //var logWriter = new IndentedTextWriter(new StreamWriter(@"c:\\log2.txt"));

            foreach (var serviceEndpoint in serviceEndpoints)
            {
                MetadataSet metadataSet = null;
                Task<MetadataSet> mexClientData = null;

                var serviceUri =
                    new Uri( serviceEndpoint.Replace("https://api.dom.gosuslugi.ru", ServicePointAddress ) + "?wsdl" );

                Console.WriteLine($"{serviceUri} start");
                
                var mexClient =
                    new MetadataExchangeClient(GetBasicHttpBinding(serviceUri.ToString()))
                    {
                        MaximumResolvedReferences = 1000,
                        HttpCredentials = new NetworkCredential(SitLogin, SitPassword),
                        ResolveMetadataReferences = true
                    };

                do
                {
                    try
                    {
                        mexClientData = mexClient.GetMetadataAsync(serviceUri, MetadataExchangeClientMode.HttpGet);
                        mexClientData.Wait();
                        metadataSet = mexClientData.Result;
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(exc.Message);
                        Console.WriteLine(serviceUri.ToString());
                    }

                } while (mexClientData == null || metadataSet == null);

                var wsdlImporter = CreateImporter(metadataSet);
                var contracts = wsdlImporter.ImportAllContracts();
                wsdlImporter.ImportAllEndpoints();
                wsdlImporter.ImportAllBindings();

                foreach (var contract in contracts)
                {
                    var generator = new ServiceContractGenerator();
                    generator.GenerateServiceContractType(contract);
                    
                    var nsname = GetHumanString(contract.Namespace);
                    generator.TargetCompileUnit.UserData.Add("ModuleName", nsname);
                    generator.TargetCompileUnit.UserData.Add("NamespaceName", globalNamespaceName.TrimEnd('.') + '.');
                    /*
                    LogDirect(contract, false);
                    LogDirect(generator.TargetCompileUnit, false);
                    */
                    concurrentDic.TryAdd(nsname, generator.TargetCompileUnit);
                }

                Console.WriteLine($"{serviceEndpoint} end");
            }

            return concurrentDic.Select(t => t.Value).ToArray();
        }

        private static WsdlImporter CreateImporter(MetadataSet metadataSet)
        {
            object dataContractImporter;
            XsdDataContractImporter xsdDcImporter;

            var options = new ImportOptions();

            var wsdlImporter = new WsdlImporter(metadataSet);

            if (!wsdlImporter.State.TryGetValue(typeof(XsdDataContractImporter), out dataContractImporter))
            {
                xsdDcImporter = new XsdDataContractImporter {
                    Options = options
                };

                wsdlImporter.State.Add( typeof(XsdDataContractImporter), xsdDcImporter );
            }
            else
            {
                xsdDcImporter = (XsdDataContractImporter)dataContractImporter;
                if (xsdDcImporter.Options == null){
                    xsdDcImporter.Options = options;
                }
            }

            var newExts = new List<IWsdlImportExtension>
            {
                new WsdlDocumentationImporter()
            };

            newExts.AddRange(wsdlImporter.WsdlImportExtensions);

            wsdlImporter = new WsdlImporter(metadataSet, wsdlImporter.PolicyImportExtensions, newExts);

            return wsdlImporter;
        }


        public static CodeCompileUnit[] GenerateCodeUnits(CodeCompileUnit[] units)
        {
            var allTypes = new Dictionary<string, List<string>>();
            var codeExportUnits = new Dictionary<string, CodeCompileUnit>();
            /*var importSettings = File.ReadAllText(@"c:\\imports.json");
            var imports = JsonConvert.DeserializeObject<Dictionary<string, List<string>>> (importSettings);*/

            //LogDirect(units, false);

            foreach (var unit in units)
            {
                //LogDirect(unit);
                foreach (var unitNamespace in unit.Namespaces.Cast<CodeNamespace>())
                {
                    foreach (var type in unitNamespace.Types.Cast<CodeTypeDeclaration>()) //- рабочая версия
                    //foreach (var type in unitNamespace.Types.Cast<CodeTypeDeclaration>().Where(t => t.Name.ToLower().Contains("importHouseRSORequestLivingHouse".ToLower())))//Версия для теста
                    //foreach (var type in unitNamespace.Types.Cast<CodeTypeDeclaration>().Where(t => t.Name.ToLower().Contains("ExportPaymentDocumentDetailsResult".ToLower())))//Версия для теста
                    //foreach (var type in unitNamespace.Types.Cast<CodeTypeDeclaration>().Where(t => t.Name.ToLower().Equals("importPaymentDocumentRequest".ToLower())))//Версия для теста
                    {
                        if (type.IsEnum && type.Name.StartsWith("Item")) continue;

                        //LogDirect(type, false);

                        ProcessType(type);

                        type.UserData["ModuleName"] = unit.UserData["ModuleName"];

                        var nsName = GetNamespaceFromAttributes(type);

                        type.UserData["FullTypeName"] = unit.UserData["NamespaceName"] + nsName + '.' + type.Name;

                        //LogDirect(type, false);

                        if (!codeExportUnits.ContainsKey(nsName))
                        {
                            var codeNamespace = new CodeNamespace("");

                            /*if(imports.ContainsKey(nsname))
                                codeNamespace.Imports.AddRange(imports[nsname].Select(t => new CodeNamespaceImport(t)).ToArray());*/

                            allTypes.Add(nsName, new List<string>());
                            codeExportUnits.Add(nsName, new CodeCompileUnit
                            {
                                Namespaces = { codeNamespace, new CodeNamespace(unit.UserData["NamespaceName"] + nsName) },
                                UserData = { { "ModuleName", nsName }, { "NamespaceName" , unit.UserData["NamespaceName"] } }//Не типы сервисов, а общие типы, которые присутствуют в модулях и у них одно и то же имя
                            });
                        }

                        if (allTypes[nsName].Contains(type.Name)) continue;

                        allTypes[nsName].Add(type.Name);
                        codeExportUnits[nsName].Namespaces[1].Types.Add(type);
                    }
                }
            }

            return codeExportUnits.Select(t => t.Value).ToArray();
        }

        /// <summary>
        /// Создаёт файловое окружение для сервисов
        /// </summary>
        /// <param name="units"></param>
        /// <param name="path"></param>
        /// <param name="isVirtual"></param>
        public static void CreateServiceSupportWithUnits(CodeCompileUnit[] units, string path, bool isVirtual=false)
        {
            var codeDomProvider = CodeDomProvider.CreateProvider("C#");

            foreach (var unit in units)
            {
                var exportFileName = unit.UserData["ModuleName"].ToString().Split('.').Last() + ".cs";
                using (
                    var myTextWriter = new AbstractIndentedTextWriter
                    (new StreamWriter(path.TrimEnd('\\') + '\\' + exportFileName), isVirtual: isVirtual))
                {
                    codeDomProvider.GenerateCodeFromCompileUnit(unit, myTextWriter, GetOptions);
                    myTextWriter.Flush(); myTextWriter.Close();
                }
            }
        }

        static Dictionary<string, CodeTypeDeclaration> map2Types_Level_One(CodeCompileUnit[] mappedUnits)
        {
            var mapperTypes = new Dictionary<string, CodeTypeDeclaration>();
            foreach (var mapUnit in mappedUnits)
            {
                foreach (var nsType in mapUnit.Namespaces[1].Types.Cast<CodeTypeDeclaration>())
                {
                    var bType = nsType.UserData["FullTypeName"].ToString();

                    if (!mapperTypes.ContainsKey(bType))
                    {
                        if (!nsType.UserData.Contains(bType))
                        {
                            var ns = mapUnit.UserData["NamespaceName"].ToString();
                            nsType.UserData.Add("FunctionName", bType.Replace(ns, ""));
                            nsType.UserData.Add("NamespaceName", ns.TrimEnd('.'));
                        }
                        mapperTypes.Add(bType, nsType);
                    }
                }
            }

            return mapperTypes;
        }

        public static CodeCompileUnit[] MapUnits(CodeCompileUnit[] mappedUnits)
        {
            /*var types = map2Types_Level_One(mappedUnits);
            CvtAndSave2Json(types, @"C:\scheme.json");*/

            var newMappingUnits = new List<CodeCompileUnit>();

            foreach (var ns in mappedUnits
                .Where(t => false == t.UserData["ModuleName"].ToString().Equals("Xmldsig")).Select(t => t.Namespaces))
            {
                var typesFromNamespace = ns[1].Types.Cast<CodeTypeDeclaration>()
                    .Where(t => t.IsClass && 
                    !t.CustomAttributes.Cast<CodeAttributeDeclaration>().Any(ca=>ca.Name.Equals("System.ServiceModel.MessageContractAttribute")) &&
                    !(t.BaseTypes.Cast<CodeTypeReference>().Any(ctr => ctr.BaseType.Equals("HeaderType")||
                        ctr.BaseType.IndexOf("System.ServiceModel.ClientBase", StringComparison.Ordinal) == 0)) &&
                        !t.Name.Equals("HeaderType") &&
                        !t.Name.Equals("BaseType") &&
                        !t.Name.Equals("Fault") && t.IsClass
                    ).ToArray();
                
                if(!typesFromNamespace.Any()) continue;

                var newNs = new CodeNamespace(ns[1].Name + "Dto");
                //var nhMappedTypes = SetNhibernateMapping(typesFromNamespace.DeepClone(), types);
                //newNs.Types.AddRange(nhMappedTypes);

                newMappingUnits.Add(new CodeCompileUnit
                {
                    Namespaces = { new CodeNamespace(), newNs }, UserData = { { "ModuleName", ns[1].Name + "Dto"} }
                });
                
            }
            
            return newMappingUnits.ToArray();
        }

        private static void CvtAndSave2Json(Dictionary<string, CodeTypeDeclaration> map2TypesLevelOne, string fileName, bool bAppend = false)
        {
            var jsonObj = JsonConvert.SerializeObject(map2TypesLevelOne);//Prepare

            using (var jsonWriter = new JsonTextWriter(new StreamWriter(fileName, bAppend)))
            {
                jsonWriter.WriteRaw(jsonObj);
                jsonWriter.Flush(); jsonWriter.Close();
            }
        }

        private static CodeTypeDeclaration[] SetNhibernateMapping(CodeTypeDeclaration[] deepClone, Dictionary<string, CodeTypeDeclaration> types)
        {
            if(!deepClone.Any())
                return deepClone;

            var newTypes = new List<CodeTypeDeclaration>();
            var changedTypes = Change2DtoClass(deepClone, types);
            newTypes.AddRange(changedTypes);//Changes in class
            newTypes.AddRange(Change2DtoMapClass(changedTypes.DeepClone(), types));//FluentNHMapping declaration

            return newTypes.ToArray();
        }

        /// <summary>
        /// Изменяет описание класса - максимально приближённому к описанию DTO объекта
        /// </summary>
        /// <param name="deepClone"></param>
        /// <returns></returns>
        private static CodeTypeDeclaration[] Change2DtoClass(CodeTypeDeclaration[] deepClone, Dictionary<string, CodeTypeDeclaration> types)
        {
            foreach (var t in deepClone)
            {
                t.CustomAttributes.Clear();
                t.Name += "Dto";

                foreach (var tMember in t.Members.OfType<CodeMemberProperty>())
                {
                    tMember.CustomAttributes.Clear();
                    //Проверяю, что тип - не перечисление, а класс
                    if (!types.Any(tm => tm.Value.Name.Equals(tMember.Type.BaseType) && tm.Value.IsClass)) continue;
                    if (tMember.Type.ArrayRank > 0)
                    {
                        var bt = tMember.Type.BaseType;
                        tMember.Type.ArrayRank = 0;
                        tMember.Type.ArrayElementType = null;
                        tMember.Type.BaseType = bt + "Dto";
                        tMember.Type = tMember.Type.BaseType.Equals("System.String") ? tMember.Type : new CodeTypeReference("IList", tMember.Type);
                        continue;
                    }

                    tMember.Type.BaseType += "Dto";
                }

                if (t.BaseTypes.Any() && types.Any(tt => tt.Value.Name.Equals(t.BaseTypes.Cast<CodeTypeReference>().First().BaseType)))
                {
                    var bt = t.BaseTypes.Cast<CodeTypeReference>().First();

                    bt.BaseType += "Dto";
                    bt.BaseType = bt.BaseType.Replace("BaseType", "Entity");
                    continue;
                }

                t.BaseTypes.Add(new CodeTypeReference("EntityDto"));
            }

            return deepClone.Where(dc => dc.Name.Contains("Dto")).ToArray();
        }

        /// <summary>
        /// Генерирует мапинг на изменённый класс
        /// </summary>
        /// <param name="deepClone"></param>
        /// <returns></returns>
        private static CodeTypeDeclaration[] Change2DtoMapClass(CodeTypeDeclaration[] deepClone, Dictionary<string, CodeTypeDeclaration> types)
        {
            foreach (var t in types.Values.Where(tv => deepClone.Any(c=>tv.Name + "Dto" == c.Name)))//deepClone.Select(p => types.Where(m => m.Value.Name.Equals(p.Name.Replace("Dto", "")))))
            {
                var theType = deepClone.First(p => p.Name == t.Name + "Dto");
                theType.CustomAttributes.Clear();

                //t.Members.Clear();
                theType.Attributes = MemberAttributes.Public;
                theType.BaseTypes.Clear();
                theType.BaseTypes.Add(new CodeTypeReference("MapAction", new CodeTypeReference(t.Name+"Dto")));
                theType.Name += "Map";

                var param = t.UserData["FunctionName"].ToString().Split('.');

                var ctor = new CodeConstructor
                {
                    Attributes = MemberAttributes.Public,
                    BaseConstructorArgs =
                    {
                        new CodeArgumentReferenceExpression(
                            $"\"{param.First()}\""),
                        new CodeArgumentReferenceExpression(
                            $"\"{param.Last()}\""),
                        new CodeArgumentReferenceExpression("id => id.Id")
                    }
                };

                foreach (var member in theType.Members.OfType<CodeMemberProperty>())
                {
                    var isMap = types.Where(p => (p.Key+" ").Contains('.'+member.Type.BaseType+" ")).Any(pt => pt.Value.IsEnum);

                    if (member.Type.BaseType.IndexOf("System.", StringComparison.Ordinal) == 0 || isMap)//CommonLanguageRuntimeType
                    {
                        var mapTypeExpression = new CodeMethodInvokeExpression(null, "Map",
                            new CodeArgumentReferenceExpression($"map => map.{member.Name}"));
                        var customTypeExpression = new CodeMethodInvokeExpression(mapTypeExpression, "CustomType<int>");//Добавление преобразования типа при SByte
                        ctor.Statements.Add( new CodeExpressionStatement(member.Type.BaseType.Equals("System.SByte") ? customTypeExpression : mapTypeExpression) );

                        continue;
                    }

                    if (member.Type.BaseType.Contains("IList"))//IList - SetThisColumnKey(HasMany(j => j.[PropName]).Cascade.All());
                    {
                        ctor.Statements.Add(
                            new CodeExpressionStatement(
                                new CodeMethodInvokeExpression(null, "SetThisColumnKey", new CodeArgumentReferenceExpression($"HasMany(hm => hm.{member.Name}).Cascade.All()"))));
                        continue;
                    }

                    ctor.Statements.Add(
                        new CodeExpressionStatement(
                            new CodeMethodInvokeExpression(null, "References", new CodeArgumentReferenceExpression($"r => r.{member.Name}"))));
                }

                theType.Members.Clear();
                theType.Members.Add(ctor);

            }

            return deepClone;
        }

        /// <summary>
        /// Генерируем уже маппинг для выбранного класса
        /// </summary>
        /// <param name="sampleType"></param>
        /// <returns></returns>
        private static CodeTypeDeclaration GenerateMappingForGivenType(CodeTypeDeclaration sampleType)
        {
            //var ctd = new CodeTypeDeclaration(sampleType.Name + "Map")
            //{
            //    Attributes = MemberAttributes.Public,
            //    IsClass = true,
            //    BaseTypes = { new CodeTypeReference("MapAction", new CodeTypeReference(sampleType.Name)) },
            //    Members = {
            //        new CodeConstructor{
            //            Attributes = MemberAttributes.Public,
            //            BaseConstructorArgs =
            //            {
            //                new CodeArgumentReferenceExpression($"\"{NormalizeStringForNhEntity(sampleType.UserData["contract_name"].ToString())}\""),
            //                new CodeArgumentReferenceExpression($"\"{NormalizeStringForNhEntity(sampleType.Name.Replace("Dto", ""))}\""),
            //                new CodeArgumentReferenceExpression("id => id.Id")
            //            }
            //        }
            //    }
            //};

            //var ctor = ctd.Members.Cast<CodeConstructor>().First();

            //foreach (var member in sampleType.Members.Cast<CodeMemberProperty>())
            //{

            //    if (member.Type.BaseType.IndexOf("System.", StringComparison.Ordinal) == 0)//CommonLanguageRuntimeType
            //    {
            //        ctor.Statements.Add(
            //            new CodeExpressionStatement(
            //                new CodeMethodInvokeExpression(null, "Map", new CodeArgumentReferenceExpression($"map => map.{member.Name}"))));
            //        continue;
            //    }

            //    if (member.Type.BaseType.Contains("IList"))//IList - SetThisColumnKey(HasMany(j => j.[PropName]).Cascade.All());
            //    {
            //        ctor.Statements.Add(
            //            new CodeExpressionStatement(
            //                new CodeMethodInvokeExpression(null, "SetThisColumnKey", new CodeArgumentReferenceExpression($"HasMany(hm => hm.{member.Name}).Cascade.All()"))));
            //        continue;
            //    }

            //    ctor.Statements.Add(
            //        new CodeExpressionStatement(
            //            new CodeMethodInvokeExpression(null, "References", new CodeArgumentReferenceExpression($"r => r.{member.Name}"))));
            //}

            //return ctd;
            return new CodeTypeDeclaration();
        }
    }
}

/*var mappingScheme = new List<GroupClass>();

            var baseEntity = CreateBaseEntity();

            foreach (var mapUnit in mappedUnits)
            {
                if( !mapUnit.UserData.Contains("ModuleName") || 
                    string.IsNullOrEmpty(mapUnit.UserData["ModuleName"].ToString()) *//*|| 
                    !mapUnit.UserData["ModuleName"].ToString().Contains('.')*//*) continue;
                var p0 = mapUnit.UserData["ModuleName"].ToString();

                if (mappingScheme.Any(t => t.Name.Equals(p0)) == false)
                {//Мапинги для пространств имён
                    mappingScheme.Add(new GroupClass(p0));
                }

                //mappingScheme[unitName]
                Console.WriteLine(p0);
                foreach (var mapNs in mapUnit.Namespaces.Cast<CodeNamespace>().Where(t => !string.IsNullOrEmpty(t.Name)))
                {
                    var p1 = $"{p0}.{mapNs.Name}";
                    if (!mappingScheme.Any(t => t.Name.Equals(p1)))
                    {
                        mappingScheme.Add(new GroupClass(p1));
                    }

                    Console.WriteLine(mapNs.Name);
                    foreach (var mapType in mapNs.Types.Cast<CodeTypeDeclaration>().Where(t => !string.IsNullOrEmpty(t.Name)))
                    {
                        var p2 = $"{p1}.{mapType.Name}";
                        if (!mappingScheme.Any(t => t.Name.Equals(p2)))
                        {
                            mappingScheme.Add(new GroupClass(p2));
                        }

                        mappingScheme.First(t => t.Name.Equals(p2)).Members.AddRange(mapType.Members.OfType<CodeMemberProperty>().Select(t=>new GroupClass(t.Name)).ToArray());

                        Console.WriteLine($"{mapType.Name} :-> {mapType.Name}Dto");
                        //foreach (var member in mapType.Members.Cast<CodeTypeMember>().Where(m => !string.IsNullOrEmpty(m.Name)))
                        //{
                        //    var p3 = $"{p2}.{member.Name}";
                        //    if (!mappingScheme.Any(t => t.Name.Equals(p3)))
                        //    {
                        //        mappingScheme.Add(new GroupClass(p3));
                        //    }
                        //}

                        AppendBaseType(mapType, baseEntity);
mapType.Name += "Dto";
                    }

                    var arrayEls = mapNs.Types.Cast<CodeTypeDeclaration>().ToArray();

var mappedTypes = SetNhibernateMapping(arrayEls);

mapNs.Types.AddRange(mappedTypes);

                    if(!mapNs.Name.Equals("Base")) continue;
                    mapNs.Types.Add(baseEntity);
                }*/