using System;
using System.CodeDom.Compiler;
using System.IO;

namespace Svc2CodeConverter
{
    public class AbstractIndentedTextWriter : IndentedTextWriter
    {
        private bool IsVirtual { get; set; }
        private bool IsSetProperties { get; set; }
        public AbstractIndentedTextWriter(TextWriter writer, bool isVirtual, bool setProperties = true) : base(writer)
        {
            IsVirtual = isVirtual;
            IsSetProperties = setProperties;
        }

        public AbstractIndentedTextWriter(TextWriter writer, string tabString) : base(writer, tabString) { }

        public override void Write(string s)
        {
            if (s.IndexOf("abstract", StringComparison.Ordinal) >= 0)
            {
                base.Write(s.Replace("abstract ", IsVirtual ? "virtual " : ""));
                return;
            }
            base.Write(s);
        }

        public override void WriteLine(string s)
        {
            if (IsSetProperties)
            {
                base.WriteLine(s);
                return;
            }

            if (s.Equals(";"))
            {
                base.WriteLine(" { get; set; } ");
                return;
            }


        }
    }
}
