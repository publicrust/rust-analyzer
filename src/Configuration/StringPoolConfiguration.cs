using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RustAnalyzer.src.Models.StringPool;
using RustAnalyzer.src.Services;
using RustAnalyzer.src.StringPool.Interfaces;
using RustAnalyzer.Utils;

namespace RustAnalyzer.src.Configuration
{
    public static class StringPoolConfiguration
    {
        private static IStringPoolProvider? _currentProvider;
        private static Dictionary<string, uint> _toNumber = new();
        private static readonly Dictionary<
            (string TypeName, string PropertyName),
            PropertyConfig
        > PropertyConfigs = new();
        private static readonly Dictionary<
            (string TypeName, string MethodName),
            MethodConfig
        > MethodConfigs = new();

        public static void Initialize(IStringPoolProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            try
            {
                _currentProvider = provider;
                _toNumber = provider.GetToNumber();
                InitializePropertyConfigs();
                InitializeMethodConfigs();
            }
            catch (Exception)
            {
                _currentProvider = null;
                _toNumber = new Dictionary<string, uint>();
            }
        }

        private static void InitializePropertyConfigs()
        {
            var configs = new List<PropertyConfig>
            {
                new PropertyConfig("BaseNetworkable", "PrefabName", PrefabNameCheckType.FullPath),
                new PropertyConfig(
                    "BaseNetworkable",
                    "ShortPrefabName",
                    PrefabNameCheckType.ShortName
                ),
                new PropertyConfig("ItemDefinition", "shortname", PrefabNameCheckType.ShortName),
            };

            foreach (var config in configs)
            {
                PropertyConfigs[(config.TypeName, config.PropertyName)] = config;
            }
        }

        private static void InitializeMethodConfigs()
        {
            var configs = new List<MethodConfig>
            {
                new MethodConfig(
                    "GameManager",
                    "CreateEntity",
                    new List<int> { 0 },
                    PrefabNameCheckType.FullPath
                ),
                new MethodConfig(
                    "BaseEntity",
                    "Spawn",
                    new List<int>(),
                    PrefabNameCheckType.FullPath
                ),
                new MethodConfig(
                    "PrefabAttribute",
                    "server",
                    new List<int>(),
                    PrefabNameCheckType.FullPath
                ),
                new MethodConfig(
                    "PrefabAttribute",
                    "client",
                    new List<int>(),
                    PrefabNameCheckType.FullPath
                ),
                new MethodConfig(
                    "GameManifest",
                    "PathToStringID",
                    new List<int> { 0 },
                    PrefabNameCheckType.FullPath
                ),
                new MethodConfig(
                    "StringPool",
                    "Add",
                    new List<int> { 0 },
                    PrefabNameCheckType.FullPath
                ),
                new MethodConfig(
                    "GameManager",
                    "FindPrefab",
                    new List<int> { 0 },
                    PrefabNameCheckType.FullPath
                ),
                new MethodConfig(
                    "ItemManager",
                    "CreateByName",
                    new List<int> { 0 },
                    PrefabNameCheckType.ShortName
                ),
                new MethodConfig(
                    "ItemManager",
                    "FindItemDefinition",
                    new List<int> { 0 },
                    PrefabNameCheckType.ShortName
                ),
                new MethodConfig(
                    "GameManager",
                    "LoadPrefab",
                    new List<int> { 0 },
                    PrefabNameCheckType.FullPath
                ),
                new MethodConfig(
                    "PrefabAttribute",
                    "Find",
                    new List<int> { 0 },
                    PrefabNameCheckType.FullPath
                ),
                new MethodConfig(
                    "StringPool",
                    "Get",
                    new List<int> { 0 },
                    PrefabNameCheckType.FullPath
                ),
            };

            foreach (var config in configs)
            {
                MethodConfigs[(config.TypeName, config.MethodName)] = config;
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

        public static string? CurrentVersion => _currentProvider?.Version;

        public static Dictionary<string, uint> ToNumber => _toNumber;

        public static PropertyConfig? GetPropertyConfig(string typeName, string propertyName)
        {
            return PropertyConfigs.TryGetValue((typeName, propertyName), out var config)
                ? config
                : null;
        }

        public static MethodConfig? GetMethodConfig(string typeName, string methodName)
        {
            return MethodConfigs.TryGetValue((typeName, methodName), out var config)
                ? config
                : null;
        }
    }
}
