using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using RustAnalyzer.Models;
using RustAnalyzer.Utils;

namespace RustAnalyzer.src.Configuration
{
    public static class DeprecatedHooksConfiguration
    {
        private static List<DeprecatedHookModel> _hooks = new();

        public static void Initialize(Dictionary<string, string> deprecatedHooks)
        {
            if (deprecatedHooks == null)
            {
                throw new ArgumentNullException(nameof(deprecatedHooks));
            }

            var hooks = new List<DeprecatedHookModel>();

            foreach (var pair in deprecatedHooks)
            {
                var oldHook = HooksUtils.ParseHookString(pair.Key);
                if (oldHook == null)
                    continue;

                MethodSignatureModel? newHook = null;
                if (!string.IsNullOrWhiteSpace(pair.Value))
                {
                    newHook = HooksUtils.ParseHookString(pair.Value);
                }

                hooks.Add(new DeprecatedHookModel { OldHook = oldHook, NewHook = newHook });
            }

            _hooks = hooks;
            Console.WriteLine($"[RustAnalyzer] Loaded {hooks.Count} deprecated hooks");
        }

        public static bool IsHook(IMethodSymbol method, out DeprecatedHookModel? hookInfo)
        {
            hookInfo = null;

            if (method == null || !HooksUtils.IsRustClass(method.ContainingType))
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            hookInfo = _hooks.FirstOrDefault(h => h.OldHook.Name == methodSignature.Name);
            return hookInfo != null;
        }

        public static bool IsHook(IMethodSymbol method)
        {
            return IsHook(method, out _);
        }
    }
}
