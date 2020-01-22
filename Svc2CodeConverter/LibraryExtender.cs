using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using FluentNHibernate.Utils;
using Newtonsoft.Json;
using NHibernate.Util;

namespace Svc2CodeConverter
{
    public class LibraryExtender
    {
        public static CodeGeneratorOptions GetOptions => new CodeGeneratorOptions {
            BracingStyle = "C",
            BlankLinesBetweenMembers = true,
            VerbatimOrder = true
        };

        public static BasicHttpBinding GetBasicHttpBinding(string serviceUri)
        {
            var isHttps = 0 == serviceUri.IndexOf("https://", StringComparison.Ordinal);

            return new BasicHttpBinding
            {
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                OpenTimeout = new TimeSpan(0, 0, 10, 0),
                SendTimeout = new TimeSpan(0, 0, 10, 0),
                ReceiveTimeout = new TimeSpan(0, 0, 10, 0),
                CloseTimeout = new TimeSpan(0, 0, 10, 0),
                AllowCookies = false,
                ReaderQuotas = new XmlDictionaryReaderQuotas
                {
                    MaxNameTableCharCount = int.MaxValue,
                    MaxStringContentLength = int.MaxValue,
                    MaxArrayLength = 32768,
                    MaxBytesPerRead = 4096,
                    MaxDepth = 32
                },
                Security = {
                        Mode = isHttps ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None,
                        Transport =
                        {
                            ClientCredentialType = isHttps ? HttpClientCredentialType.Certificate : HttpClientCredentialType.Basic
                        }
                    }
            };
        }

        /// <summary>
        /// Расщепляет атрибуты подстановочных типов классов на реальные поля в классе
        /// 
        /// Расщепляет метод, на тип и имя каждого атрибута - создаёт новые
        /// </summary>
        /// <param name="property"></param>
        /// <param name="typeName"></param>
        /// <returns></returns>
        private static IEnumerable<CodeMemberProperty> ProcessChoiceMember(CodeMemberProperty property, string typeName)
        {
            /*var hasChoise = property.CustomAttributes
                .Cast<CodeAttributeDeclaration>()
                .Any(t => t.Name.Equals("System.Xml.Serialization.XmlChoiceIdentifierAttribute"))
                || ((property.Name.IndexOf("Item", StringComparison.Ordinal) == 0) && (property.Type.BaseType.Equals("System.String") || property.Type.BaseType.Equals("System.Object")))
                || property.Type.BaseType.Equals("GKN_EGRP_KeyType") || property.Type.BaseType.Equals("GKN_EGRP_KeyRSOType");*/

            var hasChoise =
                property.CustomAttributes.Cast<CodeAttributeDeclaration>()
                    .Count(ca => ca.Name.Equals("System.Xml.Serialization.XmlElementAttribute")) > 1;

            /*Проверить, что тип есть*/
            if (!hasChoise) return new List<CodeMemberProperty> { property }.ToArray();
            
            var splittedMembers = new List<CodeMemberProperty>();

            foreach (var cad in property.CustomAttributes.Cast<CodeAttributeDeclaration>())
            {
                var argsAttrs = cad.Arguments.Cast<CodeAttributeArgument>().ToArray();
                if(argsAttrs.Length <= 1) continue;

                cad.Arguments.Clear();
                cad.Arguments.AddRange(argsAttrs.Where(aa => aa.Value is CodeTypeOfExpression == false).ToArray());

                var newTypeName =
                    argsAttrs.Select(t => t.Value).OfType<CodePrimitiveExpression>().First().Value.ToString();

                var typeArgument = argsAttrs.Select(t => t.Value).OfType<CodeTypeOfExpression>().First();
                
                var newProperty = new CodeMemberProperty
                {
                    Attributes = property.Attributes,
                    CustomAttributes = new CodeAttributeDeclarationCollection { cad },
                    Name = newTypeName + (newTypeName.Equals(typeName) ? "Type" : ""),
                    Type = typeArgument.Type,
                    HasGet = true,
                    HasSet = true
                };

                if (property.Type.ArrayElementType != null)
                {
                    newProperty.Type.ArrayElementType = null;
                    newProperty.Type.ArrayElementType = newProperty.Type.DeepClone();
                    newProperty.Type.ArrayRank = 1;
                }

                splittedMembers.Add(newProperty);
                
                if (IsNotNullable(newProperty.Type.BaseType) && property.Type.ArrayElementType == null)
                {
                    splittedMembers.Add(CreateSpecifiedForProperty(newProperty));
                }
            }
            
            return splittedMembers.ToArray();
        }

        public static CodeMemberProperty CreateSpecifiedForProperty(CodeMemberProperty property)
        {
            return new CodeMemberProperty
            {
                Attributes = property.Attributes,
                CustomAttributes = GetAttributesCollectionForSpecifiedField(),
                Name = property.Name + "Specified",
                Type = new CodeTypeReference(typeof (bool)),
                HasGet = true,
                HasSet = true
            };
        }

        static CodeAttributeDeclarationCollection GetAttributesCollectionForSpecifiedField()
        {
            return new CodeAttributeDeclarationCollection
            {
                new CodeAttributeDeclaration{
                    Name = "System.Xml.Serialization.XmlIgnoreAttribute"
                }
            };
        }

        static bool IsNotNullable(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null) return false;
            return 
                type.IsValueType ||                         // ref-type
                Nullable.GetUnderlyingType(type) == null;   // Nullable<T>
        }

        /// <summary>
        /// Обработка типа (класса в сборке)
        /// </summary>
        /// <param name="type"></param>
        internal static void ProcessType(CodeTypeDeclaration type)
        {
            type.IsPartial = false;
            RenameFieldInConstructor(type);
            ProcessTypeMembers(type);

            /*Поправляем тип FAULT*/
            if (!type.IsInterface) return;/*Этот тип только в интерфейсе*/

            foreach (var method in type.Members.OfType<CodeMemberMethod>())
            {
                method.CustomAttributes = SortCustomAttributes(method.CustomAttributes);

                foreach (var custAttribute in method.CustomAttributes.Cast<CodeAttributeDeclaration>().Where(t => t.Name.Contains("FaultContractAttribute")))
                {
                    var ca = custAttribute.Arguments.Cast<CodeAttributeArgument>().Select(t => t.Value).OfType<CodeTypeOfExpression>().First().Type.BaseType;
                    ca = ca.Replace("dom.gosuslugi.ru.schema.integration.base.", "");
                    custAttribute.Arguments.Cast<CodeAttributeArgument>()
                        .Select(t => t.Value)
                        .OfType<CodeTypeOfExpression>()
                        .First()
                        .Type.BaseType = ca;
                }

                if (!method.ReturnType.BaseType.EndsWith("Message"))
                    method.ReturnType.BaseType += "Message";

                foreach (var parameter in method.Parameters.Cast<CodeParameterDeclarationExpression>())
                {
                    parameter.Type.BaseType += "Message";
                }
            }
        }

        /// <summary>
        /// Обработка полей, методов, свойств типа
        /// </summary>
        /// <param name="type"></param>
        public static void ProcessTypeMembers(CodeTypeDeclaration type)
        {
            var newMembersList = new List<CodeTypeMember>();

            /*Обработка для Property*/
            /*Попалась одна проблема с приватными свойствами - нужно, чтобы свойство было и если поле, то имя не должно содержать field*/
            foreach (var member in type.Members.Cast<CodeTypeMember>().Where(m => m.Attributes != MemberAttributes.Private)/*.Where(tm => tm is CodeMemberProperty || tm.Name.ToLower().Contains("field"))*/)
            {
                if(member.CustomAttributes.Cast<CodeAttributeDeclaration>().Any(ca => ca.Name.Equals("System.Xml.Serialization.XmlIgnoreAttribute"))) continue;
                if (member is CodeMemberProperty)
                {
                    var castedMember = member as CodeMemberProperty;

                    if (castedMember.Type.ArrayElementType != null && castedMember.Type.ArrayElementType.ArrayRank > 0)
                    {
                        castedMember.Type.ArrayElementType.ArrayRank = 0;
                    }
                    
                    var newMember = new CodeMemberProperty
                    {
                        Attributes = (MemberAttributes) 24577, //MemberAttributes.Abstract | MemberAttributes.Public
                        CustomAttributes = member.CustomAttributes,
                        Name = castedMember.Name,
                        Type = castedMember.Type,
                        HasGet = true, HasSet = true,
                    };

                    var additionalTypes = ProcessChoiceMember(newMember, type.Name);
                    newMembersList.AddRange(additionalTypes);
                    continue;
                }
                
                newMembersList.Add(member);
            }

            var order = 0;
            foreach (var newMember in newMembersList)
            {
                foreach (var cad in newMember.CustomAttributes.Cast<CodeAttributeDeclaration>().Where(ca => !ca.Name.Equals("System.Xml.Serialization.XmlAnyElementAttribute")))
                {
                    var argsAttrs = cad.Arguments.Cast<CodeAttributeArgument>().ToList();
                    if (!argsAttrs.Any(w => w.Name.Equals("Order"))) continue;
                    var orderAttrParameter = argsAttrs.Select(t => t).First(w => w.Name.Equals("Order"));
                    orderAttrParameter.Value = new CodePrimitiveExpression(order);
                    order++;
                }
            }

            /*var methods =
                type.Members.Cast<CodeTypeMember>().Where(tm => !(tm is CodeMemberProperty)).Select(t => t).ToArray();*/

            type.Members.Clear();
            type.Members.AddRange(newMembersList.ToArray());
            //type.Members.AddRange(methods);
        }

        private void Add2CodeSnippet(string snippetTemplate, CodeTypeDeclaration type)
        {
            var code = "";
        }

        private static CodeAttributeDeclarationCollection SortCustomAttributes(CodeAttributeDeclarationCollection customAttributes)
        {
            var collection = new CodeAttributeDeclarationCollection();
            collection.AddRange(customAttributes.Cast<CodeAttributeDeclaration>().Where(t => t.Name.Equals("System.ServiceModel.ServiceKnownTypeAttribute") == false).ToArray());
            collection.AddRange(customAttributes.Cast<CodeAttributeDeclaration>().Where(t => t.Name.Equals("System.ServiceModel.ServiceKnownTypeAttribute"))
                    .OrderBy(a => a.Arguments.Cast<CodeAttributeArgument>()
                    .Select(t => t.Value).OfType<CodeTypeOfExpression>().First().Type.BaseType).ToArray());

            return collection;
        }

        private static void RenameFieldInConstructor(CodeTypeDeclaration type)
        {
            foreach (var ctor in type.Members.OfType<CodeConstructor>())
            {
                foreach (var field in ctor.Statements.OfType<CodeAssignStatement>())
                {
                    var fieldLeft = field?.Left as CodeFieldReferenceExpression;
                    if (fieldLeft == null) continue;

                    foreach (var hasProp in type.Members.OfType<CodeMemberProperty>())
                    {
                        if(!hasProp.Name.ToLower().Equals(fieldLeft.FieldName.ToLower().Replace("field", ""))) continue;
                        fieldLeft.FieldName = hasProp.Name;
                    }
                }
            }
        }

        /// <summary>
        /// Из атрибутов типа вытаскиваю пространство имён для него, которое ему присвоили в GIS
        /// </summary>
        /// <param name="type">unit.UserData["ModuleName"] + "." + type.Name</param>
        /// <returns></returns>
        public static string GetNamespaceFromAttributes(CodeTypeDeclaration type)
        {
            var nsname = string.Empty;

            var codeAttributeDeclarations = type.CustomAttributes.Cast<CodeAttributeDeclaration>().ToArray();

            if (codeAttributeDeclarations.Any(an => an.Name.Contains("MessageContractAttribute")))
            {
                type.Name = type.Name + "Message";
            }

            foreach (var attr in codeAttributeDeclarations)
            {
                foreach (var arg in attr.Arguments.Cast<CodeAttributeArgument>().Where(arg => arg.Name.Equals("Namespace")))
                {
                    var val = arg.Value as CodePrimitiveExpression;
                    if (val != null) nsname = val.Value.ToString();
                    if(!string.IsNullOrEmpty(nsname)) break;
                }
                if (!string.IsNullOrEmpty(nsname)) break;
            }

            var nsName = string.IsNullOrEmpty(nsname)
                ? type.UserData["ModuleName"].ToString()
                : GetHumanString(nsname);
            type.UserData["NamespaceName"] = nsName;

            return nsName;
        }

        public static string GetHumanString(string inStr)
        {
            var outStr = new StringBuilder();
            var s = inStr.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Last().Replace("#", "");
            var sChArray = s.ToCharArray();
            var sUpper = s.ToUpper().ToCharArray();
            var bUp = true;
            for (var ch = 0; ch < sChArray.Length; ch++)
            {
                if (sChArray[ch] == '-')
                {
                    bUp = true;
                    continue;
                }
                outStr.Append(bUp ? sUpper[ch] : sChArray[ch]);
                bUp = false;
            }
            return outStr.ToString();
        }

        public static void AppendBaseType(CodeTypeDeclaration ctd, CodeTypeDeclaration baseType, bool overwriteBaseTypeAndGo = false)
        {
            if (ctd.BaseTypes.Any())
            {
                if (overwriteBaseTypeAndGo) return;
                ctd.BaseTypes.Clear();
            };
            ctd.BaseTypes.Add(new CodeTypeReference
            {
                BaseType = baseType.Name
            });
        }

        public static void LogDirect(object object2Log, bool append = true)
        {
            const string path = @"..\log.json";
            var poJson = JsonConvert.SerializeObject(object2Log, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                MaxDepth = 10
            });

            using (var myTextWriter = new JsonTextWriter(new StreamWriter(path, append)))
            {
                myTextWriter.WriteRaw(poJson);
                myTextWriter.WriteWhitespace(Environment.NewLine);
                myTextWriter.Flush();
                myTextWriter.Close();
            }
        }

        public static void SerializeXml(XmlSchema schema)
        {
            const string path = @"..\schema_log.xml";
            var xmlSerializer = new XmlSerializer(typeof (XmlSchema));

            using (var xmlWriter = new StreamWriter(path))
            {
                xmlSerializer.Serialize(xmlWriter, schema);
                xmlWriter.Flush();
                xmlWriter.Close();
            }
        }
    }
}
