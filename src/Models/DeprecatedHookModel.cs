using System;
using System.Collections.Generic;
using System.Text;

namespace RustAnalyzer.Models
{
    public class DeprecatedHookModel
    {
        public MethodSignatureModel OldHook { get; set; }
        public MethodSignatureModel? NewHook { get; set; }
    }
}
