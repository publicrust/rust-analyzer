using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using RustAnalyzer.Models;
using RustAnalyzer.Utils;
using System.Diagnostics;
using System.Text.Json;

namespace RustAnalyzer
{
    /// <summary>
    /// Contains plugin-specific hook signatures and logic to identify them.
    /// </summary>
    public static class PluginHooksConfiguration
    {
        private static readonly ImmutableList<PluginHookModel> _hooks;

        static PluginHooksConfiguration()
        {
            try
            {
                var hooks = PluginHooksJson.GetHooks();
                _hooks = ImmutableList.CreateRange(hooks);
            }
            catch (Exception ex)
            {
                _hooks = ImmutableList<PluginHookModel>.Empty;
            }
        }

        /// <summary>
        /// Gets all configured hook signatures.
        /// </summary>
        public static ImmutableList<PluginHookModel> HookSignatures => _hooks;

        /// <summary>
        /// Checks if a given method name or signature is a known plugin hook.
        /// </summary>
        public static bool IsHook(IMethodSymbol method)
        {
            if (method == null || method.ContainingType == null ||
                !HooksUtils.IsRustClass(method.ContainingType))
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null) return false;

            return _hooks.Any(s => s.HookName == methodSignature.HookName);
        }


        /// <summary>
        /// Checks if a given method signature exactly matches a known hook signature.
        /// This method requires the full signature to match.
        /// </summary>
        public static bool IsKnownHook(IMethodSymbol method)
        {
            if (method == null || method.ContainingType == null ||
                !HooksUtils.IsRustClass(method.ContainingType))
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            return _hooks.Any(s => s.HookName == methodSignature.HookName);
        }

        /// <summary>
        /// Returns hooks with similar names to the method along with their plugin sources.
        /// </summary>
        public static IEnumerable<(string hookName, string pluginName)> GetSimilarHooks(IMethodSymbol method, int maxSuggestions = 3)
        {
            if (method == null || method.ContainingType == null ||
                !HooksUtils.IsRustClass(method.ContainingType))
                return Enumerable.Empty<(string, string)>();

            var similarHooks = _hooks
                .Select(h => (h.HookName, h.PluginName))
                .OrderBy(h => StringDistance.GetLevenshteinDistance(method.Name, h.HookName))
                .Take(maxSuggestions);

            return similarHooks;
        }

        /// <summary>
        /// Gets plugin information for a specific hook.
        /// </summary>
        public static PluginHookModel GetPluginInfo(string hookName)
        {
            return _hooks.FirstOrDefault(h => h.HookName == hookName);
        }
    }
} 