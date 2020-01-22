using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Threading.Tasks;

namespace Svc2CodeConverter.Extensions
{
    public class MyServiceContractGeneratorExtension : IServiceContractGenerationExtension, IContractBehavior
    {
        public void GenerateContract(ServiceContractGenerationContext context)
        {
            Console.WriteLine(@"MyServiceContractGeneratorExtension - GenerateContract");
            context.ContractType.Comments.Add(new CodeCommentStatement("SET ContractType COMMENT ATTRIBUTE"));
        }

        public void Validate(ContractDescription contractDescription, ServiceEndpoint endpoint)
        {
            //Console.WriteLine(@"MyServiceContractGeneratorExtension - Validate");
            var p = contractDescription;
        }

        public void ApplyDispatchBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint,
            DispatchRuntime dispatchRuntime)
        {
            //Console.WriteLine(@"MyServiceContractGeneratorExtension - ApplyDispatchBehavior");
            var cd = contractDescription;
        }

        public void ApplyClientBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            //Console.WriteLine(@"MyServiceContractGeneratorExtension - ApplyClientBehavior");
            var cd = contractDescription;
        }

        public void AddBindingParameters(ContractDescription contractDescription, ServiceEndpoint endpoint,
            BindingParameterCollection bindingParameters)
        {
            //Console.WriteLine(@"MyServiceContractGeneratorExtension - AddBindingParameters");
            var cd = contractDescription;
        }
    }
}
