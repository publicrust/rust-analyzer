using System.Collections.Generic;

namespace RustAnalyzer.src.StringPool.Interfaces
{
    public interface IStringPoolProvider
    {
        string Version { get; }
        Dictionary<string, uint> GetToNumber();
    }
} 