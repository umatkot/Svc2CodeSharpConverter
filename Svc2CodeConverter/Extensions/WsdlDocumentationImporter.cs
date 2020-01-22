using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Web.Services.Description;
using System.Xml;
using System.Xml.Schema;

namespace Svc2CodeConverter.Extensions
{
    //public class WsdlDocumentationImporter : IWsdlImportExtension
    public class WsdlDocumentationImporter : IWsdlImportExtension, IContractBehavior, IOperationBehavior
    {
        private XmlElement RootElement { get; set; }

        public WsdlDocumentationImporter(XmlElement element) { RootElement = element; }

        public WsdlDocumentationImporter() { }

        public void ImportContract(WsdlImporter importer, WsdlContractConversionContext context)
        {
            Console.WriteLine(@"ImportContract");

            // Contract documentation

            if (context.WsdlPortType.Documentation != null)
            {
                context.Contract.Behaviors.Add(new WsdlDocumentationImporter(context.WsdlPortType.DocumentationElement));
                //context.Contract.Behaviors.Add(new MyServiceContractGeneratorExtension());
            }

            // Operation documentation
            foreach (Operation operation in context.WsdlPortType.Operations.Cast<Operation>().Where(t => t.Documentation != null))
            {
                var operationDescription = context.Contract.Operations.Find(operation.Name);
                operationDescription?.Behaviors.Add(new WsdlDocumentationImporter(operation.DocumentationElement));
                //operationDescription?.Behaviors.Add(new MyOperationContractGenerationExtension());
            }
        }

        public void BeforeImport(ServiceDescriptionCollection wsdlDocuments, XmlSchemaSet xmlSchemas, ICollection<XmlElement> policy) { }

        public void ImportEndpoint(WsdlImporter importer, WsdlEndpointConversionContext context) { }

        public void Validate(ContractDescription contractDescription, ServiceEndpoint endpoint) { }

        public void ApplyDispatchBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint,
            DispatchRuntime dispatchRuntime)
        { }

        public void ApplyClientBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint,
            ClientRuntime clientRuntime)
        { }

        public void AddBindingParameters(ContractDescription contractDescription, ServiceEndpoint endpoint,
            BindingParameterCollection bindingParameters)
        { }

        public void Validate(OperationDescription operationDescription) { }

        public void ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation) { }

        public void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation) { }

        public void AddBindingParameters(OperationDescription operationDescription,
            BindingParameterCollection bindingParameters)
        { }
    }
}
