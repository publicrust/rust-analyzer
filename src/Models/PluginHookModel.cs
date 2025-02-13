using System;
using System.Collections.Generic;
using System.Text;

namespace RustAnalyzer.Models
{
    /// <summary>
    /// Represents a hook that is provided by a specific plugin.
    /// </summary>
    public class PluginHookModel : HookModel
    {
        /// <summary>
        /// Name of the plugin that provides this hook.
        /// </summary>
        public string PluginName { get; set; }
    }
}
