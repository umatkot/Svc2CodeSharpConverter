using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Svc2CodeConverter
{
    public class CodeDomProcessor : LibraryExtender
    {
        public static void TransformCodeCompileUnit(CodeCompileUnit codeCompileUnit)
        {
            foreach (var codeNamespace in codeCompileUnit.Namespaces.Cast<CodeNamespace>())
            {
                foreach (var codeType in codeNamespace.Types.Cast<CodeTypeDeclaration>())
                {
                    if (codeType.IsEnum && codeType.Name.StartsWith("Item")) continue;

                    codeType.IsPartial = false;

                    //ProcessType(codeType);
                }
            }
        }
    }
}
