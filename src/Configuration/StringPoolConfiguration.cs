using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RustAnalyzer.src.Models.StringPool;
using RustAnalyzer.Utils;

namespace RustAnalyzer.src.Configuration
{
    public static class StringPoolConfiguration
    {
        private static Dictionary<string, uint> _toNumber = new();
        private static readonly Dictionary<(string TypeName, string PropertyName), PropertyConfig> PropertyConfigs = new();
        private static readonly Dictionary<(string TypeName, string MethodName), MethodConfig> MethodConfigs = new();

        public static void Initialize(Dictionary<string, uint> stringPool)
        {
            if (stringPool == null)
            {
                throw new ArgumentNullException(nameof(stringPool));
            }

            _toNumber = stringPool;
            InitializePropertyConfigs();
            Console.WriteLine($"[RustAnalyzer] Loaded {stringPool.Count} string pool entries");
        }

        public static void InitializeMethodConfigs(List<StringPoolMethodDefinition> methodConfigs)
        {
            if (methodConfigs == null)
            {
                throw new ArgumentNullException(nameof(methodConfigs));
            }

            MethodConfigs.Clear();
            foreach (var config in methodConfigs)
            {
                var methodConfig = new MethodConfig(
                    config.TypeName,
                    config.MethodName,
                    config.ParameterIndices,
                    config.CheckType
                );
                MethodConfigs[(config.TypeName, config.MethodName)] = methodConfig;
            }

            Console.WriteLine($"[RustAnalyzer] Loaded {methodConfigs.Count} string pool method configurations");
        }

        private static void InitializePropertyConfigs()
        {
            PropertyConfigs.Clear();
            foreach (var pair in _toNumber)
            {
                var parts = pair.Key.Split('.');
                if (parts.Length != 2)
                    continue;

                var typeName = parts[0];
                var propertyName = parts[1];

                PropertyConfigs.Add(
                    (typeName, propertyName),
                    new PropertyConfig(typeName, propertyName, PrefabNameCheckType.FullPath)
                );
            }
        }

        public static bool IsValidShortName(string shortName)
        {
            shortName = shortName.ToLowerInvariant().Trim();

            if (_toNumber.Count == 0)
            {
                return true; // Временно разрешаем все значения, если конфигурация пуста
            }

            foreach (var prefabName in _toNumber.Keys)
            {
                var sn = Path.GetFileNameWithoutExtension(prefabName);
                if (sn.Equals(shortName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsValidPrefabPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            path = path.ToLowerInvariant().Replace("\\", "/").Trim();
            return _toNumber.ContainsKey(path);
        }

        public static IEnumerable<string> FindSimilarShortNames(string shortName)
        {
            shortName = shortName.ToLowerInvariant();
            var candidates = _toNumber.Keys.Select(p => Path.GetFileNameWithoutExtension(p));
            return StringSimilarity.FindSimilar(shortName, candidates);
        }

        public static IEnumerable<string> FindSimilarPrefabs(string invalidPath)
        {
            invalidPath = invalidPath.ToLowerInvariant().Replace("\\", "/").Trim();
            return StringSimilarity.FindSimilar(invalidPath, _toNumber.Keys);
        }

        public static bool TryGetPropertyConfig(string typeName, string propertyName, out PropertyConfig config)
        {
            return PropertyConfigs.TryGetValue((typeName, propertyName), out config);
        }

        public static bool TryGetMethodConfig(string typeName, string methodName, out MethodConfig config)
        {
            return MethodConfigs.TryGetValue((typeName, methodName), out config);
        }

        public static MethodConfig GetMethodConfig(string typeName, string methodName)
        {
            if (TryGetMethodConfig(typeName, methodName, out var config))
                return config;
            return null;
        }

        public static PropertyConfig GetPropertyConfig(string typeName, string propertyName)
        {
            if (TryGetPropertyConfig(typeName, propertyName, out var config))
                return config;
            return null;
        }
    }
}
