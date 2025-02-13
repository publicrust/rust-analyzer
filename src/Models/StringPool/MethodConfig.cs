using System.Collections.Generic;
using RustAnalyzer.src.Models.StringPool;

namespace RustAnalyzer.src.Models.StringPool
{
    public class MethodConfig
    {
        public string TypeName { get; }
        public string MethodName { get; }
        public List<int> ParameterIndices { get; }
        public PrefabNameCheckType CheckType { get; }

        public MethodConfig(
            string typeName,
            string methodName,
            List<int> parameterIndices,
            PrefabNameCheckType checkType = PrefabNameCheckType.FullPath
        )
        {
            TypeName = typeName;
            MethodName = methodName;
            ParameterIndices = parameterIndices;
            CheckType = checkType;
        }
    }
}
