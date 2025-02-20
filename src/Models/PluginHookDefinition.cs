using System.Text.Json.Serialization;

namespace RustAnalyzer.Models
{
    public class PluginHookDefinition
    {
        [JsonPropertyName("pluginName")]
        public string PluginName { get; set; }

        [JsonPropertyName("hookSignature")]
        public string HookSignature { get; set; }
    }
} 