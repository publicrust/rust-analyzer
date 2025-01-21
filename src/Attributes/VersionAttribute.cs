using System;

namespace RustAnalyzer.src.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class VersionAttribute : Attribute
    {
        public string Version { get; }

        public VersionAttribute(string version)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }
    }
} 