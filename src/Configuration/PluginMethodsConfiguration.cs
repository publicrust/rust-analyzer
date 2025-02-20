using System;
using System.Collections.Generic;
using System.Linq;
using RustAnalyzer.Models;
using RustAnalyzer.Utils;

namespace RustAnalyzer
{
    public class PluginMethodParameter : MethodParameter
    {
        public override bool IsOptional { get; set; }
        public override string? DefaultValue { get; set; }
    }

    public class PluginMethod : MethodSignatureModel
    {
        public string ReturnType { get; set; } = "void";

        public override string ToString()
        {
            return $"{ReturnType} {Name}({string.Join(", ", Parameters)})";
        }
    }

    public class PluginConfiguration
    {
        public string PluginName { get; }
        public Dictionary<string, PluginMethod> Methods { get; }

        public PluginConfiguration(string pluginName)
        {
            PluginName = pluginName ?? throw new ArgumentNullException(nameof(pluginName));
            Methods = new Dictionary<string, PluginMethod>();
        }
    }

    public static class PluginMethodsConfiguration
    {
        private static readonly Dictionary<string, PluginConfiguration> Configurations = new();

        public static void Initialize(List<PluginHookDefinition> methods)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            try
            {
                Configurations.Clear();
                var groupedMethods = methods.GroupBy(m => m.PluginName);

                foreach (var group in groupedMethods)
                {
                    if (string.IsNullOrEmpty(group.Key))
                    {
                        Console.WriteLine("[RustAnalyzer] Warning: Found method without plugin name");
                        continue;
                    }

                    var config = new PluginConfiguration(group.Key);

                    foreach (var hookDef in group)
                    {
                        var method = HooksUtils.ParseHookString(hookDef.HookSignature) as PluginMethod;
                        if (method == null)
                        {
                            Console.WriteLine($"[RustAnalyzer] Warning: Failed to parse hook signature: {hookDef.HookSignature}");
                            continue;
                        }
                        config.Methods[method.Name] = method;
                    }

                    Configurations[group.Key] = config;
                }

                Console.WriteLine($"[RustAnalyzer] Loaded {Configurations.Count} plugin configurations with {methods.Count} methods");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RustAnalyzer] Failed to initialize plugin methods: {ex.Message}");
                Configurations.Clear();
            }
        }

        public static PluginConfiguration? GetConfiguration(string pluginName)
        {
            return Configurations.TryGetValue(pluginName, out var config) ? config : null;
        }

        public static PluginMethod? GetMethod(string pluginName, string methodName)
        {
            var config = GetConfiguration(pluginName);
            return config?.Methods.TryGetValue(methodName, out var method) == true ? method : null;
        }

        public static bool HasPlugin(string pluginName)
        {
            return Configurations.ContainsKey(pluginName);
        }
    }
}
