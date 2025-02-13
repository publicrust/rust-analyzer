using RustAnalyzer.src.Models.StringPool;

namespace RustAnalyzer.src.Models.StringPool
{
    public class PropertyConfig
    {
        public string TypeName { get; }
        public string PropertyName { get; }
        public PrefabNameCheckType CheckType { get; }

        public PropertyConfig(
            string typeName,
            string propertyName,
            PrefabNameCheckType checkType = PrefabNameCheckType.FullPath
        )
        {
            TypeName = typeName;
            PropertyName = propertyName;
            CheckType = checkType;
        }
    }
}
