using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RustAnalyzer.Models
{
    public class HookParameter
    {
        public string Type { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? Type : $"{Type} {Name}";
        }
    }

    public class HookModel
    {
        public string HookName { get; set; }
        public List<HookParameter> HookParameters { get; set; } = new List<HookParameter>();

        public override string ToString()
        {
            return $"{HookName}({string.Join(", ", HookParameters)})";
        }
    }
}
