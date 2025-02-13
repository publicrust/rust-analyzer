using System;
using System.Reflection;
using RustAnalyzer.Models;
using RustAnalyzer.src.Attributes;
using RustAnalyzer.src.DeprecatedHooks.Interfaces;

namespace RustAnalyzer.src.DeprecatedHooks.Providers
{
    [Version("266Dev")]
    public class DeprecatedHooks266DevBlogProvider : BaseDeprecatedJsonHooksProvider
    {
        protected override string JsonContent => "{\r\n  \"deprecated\": {\r\n} }";
    }
}
