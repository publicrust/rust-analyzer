using RustAnalyzer.src.DeprecatedHooks.Interfaces;
using RustAnalyzer.Models;
using System;
using System.Reflection;
using RustAnalyzer.src.Attributes;

namespace RustAnalyzer.src.DeprecatedHooks.Providers
{
    [Version("266Dev")]
    public class DeprecatedHooks266DevBlogProvider : BaseDeprecatedJsonHooksProvider
    {
        protected override string JsonContent => "{\r\n  \"deprecated\": {\r\n} }";
    }
}