using RustAnalyzer.src.Attributes;
using RustAnalyzer.src.DeprecatedHooks.Interfaces;
using RustAnalyzer.Models;
using System;
using System.Reflection;

namespace RustAnalyzer.src.DeprecatedHooks.Providers
{
    [Version("240DevBlog")]
    public class DeprecatedHooks240DevBlogProvider : BaseDeprecatedJsonHooksProvider
    {
        protected override string JsonContent => "{\r\n  \"deprecated\": {\r\n} }";
    }
}
