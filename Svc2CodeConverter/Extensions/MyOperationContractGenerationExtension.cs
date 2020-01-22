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
    public class MyOperationContractGenerationExtension : IOperationContractGenerationExtension, IOperationBehavior
    {
        public void GenerateOperation(OperationContractGenerationContext context)
        {
            Console.WriteLine(@"MyOperationContractGenerationExtension - GenerateOperation");
            context.SyncMethod.Comments.Add(new CodeCommentStatement("SET SyncMethod COMMENT ATTRIBUTE"));
        }

        public void Validate(OperationDescription operationDescription)
        {
            //Console.WriteLine(@"MyOperationContractGenerationExtension - Validate");
            var od = operationDescription;
        }

        public void ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation)
        {
            //Console.WriteLine(@"MyOperationContractGenerationExtension - ApplyDispatchBehavior");
            var od = operationDescription;
        }

        public void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation)
        {
            //Console.WriteLine(@"MyOperationContractGenerationExtension - ApplyClientBehavior");
            var od = operationDescription;
        }

        public void AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters)
        {
            //Console.WriteLine(@"MyOperationContractGenerationExtension - AddBindingParameters");
            var od = operationDescription;
        }
    }
}
