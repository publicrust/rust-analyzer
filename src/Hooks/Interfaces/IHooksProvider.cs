using System.Collections.Generic;
using RustAnalyzer.Models;

namespace RustAnalyzer.src.Hooks.Interfaces
{
    public interface IHooksProvider
    {
        string Version { get; }
        List<HookModel> GetHooks();
    }
}
