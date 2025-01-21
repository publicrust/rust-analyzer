using RustAnalyzer.src.Attributes;
using RustAnalyzer.src.StringPool.Interfaces;

namespace RustAnalyzer.src.StringPool.Providers
{
    [Version("240Dev")]
    public class StringPool240DevBlogProvider : BaseJsonStringPoolProvider
    {
        protected override string JsonContent => @"{}";
    }
}