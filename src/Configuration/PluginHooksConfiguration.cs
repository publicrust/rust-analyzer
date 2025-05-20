using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using RustAnalyzer.Models;
using RustAnalyzer.Utils;

namespace RustAnalyzer
{
    /// <summary>
    /// Contains plugin-specific hook signatures and logic to identify them.
    /// </summary>
    public static class PluginHooksConfiguration
    {
        private static ImmutableList<HookModel> _hooks = ImmutableList<HookModel>.Empty;

        /// <summary>
        /// Initializes the configuration with plugin hooks from JSON
        /// </summary>
        public static void Initialize(List<PluginHookDefinition> pluginHooks)
        {
            if (pluginHooks == null)
            {
                throw new ArgumentNullException(nameof(pluginHooks));
            }

            try
            {
                var hooks = new List<HookModel>();
                
                foreach (var hook in pluginHooks)
                {
                    var hookSignature = HooksUtils.ParseHookString(hook.HookSignature);
                    if (hookSignature == null)
                    {
                        Console.WriteLine($"[RustAnalyzer] Warning: Failed to parse hook signature: {hook.HookSignature}");
                        continue;
                    }

                    hooks.Add(new HookModel
                    {
                        Method = new MethodSourceModel 
                        { 
                            ClassName = hook.PluginName,
                            Signature = hookSignature
                        },
                        Signature = hookSignature
                    });
                }

                _hooks = ImmutableList.CreateRange(hooks);
                Console.WriteLine($"[RustAnalyzer] Loaded {hooks.Count} plugin hooks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RustAnalyzer] Failed to initialize plugin hooks: {ex.Message}");
                _hooks = ImmutableList<HookModel>.Empty;
            }
        }

        /// <summary>
        /// Gets all configured hook signatures.
        /// </summary>
        public static ImmutableList<HookModel> HookSignatures => _hooks;

        /// <summary>
        /// Checks if a given method name or signature is a known plugin hook.
        /// </summary>
         public static bool IsHook(IMethodSymbol method)
        {
            if (method == null || !HooksUtils.IsRustClass(method.ContainingType))
                return false;

            var sig = HooksUtils.GetMethodSignature(method);

            if (sig == null || 
                sig.Name != method.Name ||
                sig.Parameters.Count != method.Parameters.Length)
            {
                return false;
            }

            var matchingHooks = _hooks.Where(h => 
                h.Signature.Name == method.Name && 
                h.Signature.Parameters.Count == method.Parameters.Length &&
                sig.Parameters.Select((p, i) => new { 
                    Index = i,
                    Expected = p.Type, 
                    ExpectedName = p.Name,
                    Actual = method.Parameters[i].Type,
                    ActualName = method.Parameters[i].Name,
                    // Проверяем только соответствие типов, игнорируем имена параметров
                    IsCompatible = HooksConfiguration.IsTypeCompatible(method.Parameters[i].Type, p.Type, method)
                }).All(x => x.IsCompatible)
            ).Any();

            return matchingHooks;
        }

        /// <summary>
        /// Checks if a given method signature exactly matches a known hook signature.
        /// This method requires the full signature to match.
        /// </summary>
        public static bool IsKnownHook(IMethodSymbol method)
        {
            if (
                method == null
                || method.ContainingType == null
                || !HooksUtils.IsRustClass(method.ContainingType)
            )
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            return _hooks.Any(s => s.Signature.Name == methodSignature.Name);
        }

        /// <summary>
        /// Returns hooks with similar names to the method along with their plugin sources.
        /// </summary>
        public static IEnumerable<HookModel> GetSimilarHooks(
            IMethodSymbol method,
            int maxSuggestions = 3
        )
        {
            if (
                method == null
                || method.ContainingType == null
                || !HooksUtils.IsRustClass(method.ContainingType)
            )
                return Enumerable.Empty<HookModel>();

            var candidates = _hooks.Select(h =>
                (
                    text: $"{h.Signature.Name}({string.Join(", ", h.Signature.Parameters.Select(p => p.Type))})",
                    context: h
                )
            );

            return StringSimilarity
                .FindSimilarWithContext(method.Name, candidates, maxSuggestions)
                .Select(r => r.Context);
        }

        /// <summary>
        /// Gets plugin information for a specific hook.
        /// </summary>
        public static HookModel GetPluginInfo(string hookName)
        {
            return _hooks.FirstOrDefault(h => h.Signature.Name == hookName);
        }
    }
}
