using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RustAnalyzer.src.Models.StringPool
{
    public class StringPoolMethodDefinition
    {
        [JsonPropertyName("typeName")]
        public string TypeName { get; set; }

        [JsonPropertyName("methodName")]
        public string MethodName { get; set; }

        [JsonPropertyName("parameterIndices")]
        public List<int> ParameterIndices { get; set; } = new List<int>();

        [JsonPropertyName("checkType")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PrefabNameCheckType CheckType { get; set; } = PrefabNameCheckType.FullPath;
    }
} 