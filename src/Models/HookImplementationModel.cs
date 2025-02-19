using System.Text.Json.Serialization;

namespace RustAnalyzer.Models
{
    public class HookImplementationModel
    {
        [JsonPropertyName("HookSignature")]
        public string HookSignature { get; set; }

        [JsonPropertyName("MethodSignature")]
        public string MethodSignature { get; set; }

        [JsonPropertyName("MethodSourseCode")]
        public string MethodSourceCode { get; set; }

        [JsonPropertyName("ClassName")]
        public string MethodClassName { get; set; }

        [JsonPropertyName("HookLineInvoke")]
        public int HookLineInvoke { get; set; }
    }
} 