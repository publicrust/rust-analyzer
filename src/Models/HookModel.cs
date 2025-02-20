using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RustAnalyzer.Models
{
    public class MethodParameter
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public virtual bool IsOptional { get; set; }
        public virtual string? DefaultValue { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? Type : $"{Type} {Name}";
        }
    }

    public class MethodSignatureModel 
    {
        public string Name { get; set; }
        public List<MethodParameter> Parameters { get; set; } = new List<MethodParameter>();

        public override string ToString()
        {
            return $"{Name}({string.Join(", ", Parameters)})";
        }
    }

    public class MethodSourceModel
    {
        public string ClassName { get; set; }
        public string SourceCode { get; set; }

        public MethodSignatureModel Signature { get; set; }
    }

    public class HookModel
    {
        public MethodSignatureModel Signature { get; set; }
        public int HookCallLine { get; set; }
        public MethodSourceModel Method { get; set; }

        public override string ToString()
        {
            return Signature.ToString();
        }
    }
}
