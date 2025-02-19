using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                Console.WriteLine($"[RustAnalyzer] {pair.Key} {pair.Value}");
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
            if (method == null)
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            foreach (var hook in _hooks)
            {
                // Проверяем имя хука
                if (hook.OldHook.Name != methodSignature.Name)
                    continue;

                // Проверяем количество параметров
                if (hook.OldHook.Parameters.Count != methodSignature.Parameters.Count)
                    continue;

                // Проверяем типы параметров
                bool allParametersMatch = true;
                for (int i = 0; i < methodSignature.Parameters.Count; i++)
                {
                    if (
                        hook.OldHook.Parameters[i].Type
                        != methodSignature.Parameters[i].Type
                    )
                    {
                        allParametersMatch = false;
                        break;
                    }
                }

                if (allParametersMatch)
                {
                    hookInfo = hook;
                    return true;
                }
            }

            return false;
        }

        public static bool IsHook(IMethodSymbol method) => IsHook(method, out _);
    }
}
