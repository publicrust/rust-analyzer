using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using RustAnalyzer.Models;
using RustAnalyzer.Utils;
using RustAnalyzer.src.Hooks.Providers;
using RustAnalyzer.src.Hooks.Interfaces;
using RustAnalyzer.Configuration;
using RustAnalyzer.src.Services;

namespace RustAnalyzer
{
    /// <summary>
    /// Contains Rust-specific hook signatures and logic to identify them.
    /// </summary>
    public static class HooksConfiguration
    {
        private static IHooksProvider? _currentProvider;
        private static ImmutableList<HookModel> _hooks = ImmutableList<HookModel>.Empty;

        /// <summary>
        /// Initializes the hooks configuration with the specified provider
        /// </summary>
        public static void Initialize(IHooksProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            try
            {
                var regularProvider = ProviderDiscovery.CreateRegularHooksProvider("Universal");
                if (regularProvider == null)
                {
                    _currentProvider = provider;
                    _hooks = ImmutableList.CreateRange(provider.GetHooks());
                    return;
                }

                var regularHooks = regularProvider.GetHooks();
                var providerHooks = provider.GetHooks();
                
                // Создаем словарь для быстрого поиска хуков по имени и параметрам
                var hookDictionary = new Dictionary<string, HookModel>();
                
                // Сначала добавляем все хуки из regularProvider
                foreach (var hook in regularHooks)
                {
                    var key = $"{hook.HookName}";
                    hookDictionary[key] = hook;
                }
                
                // Добавляем хуки из provider, только если такого хука еще нет
                foreach (var hook in providerHooks)
                {
                    var key = $"{hook.HookName}";
                    if (!hookDictionary.ContainsKey(key))
                    {
                        hookDictionary[key] = hook;
                    }
                }

                _currentProvider = provider;
                _hooks = ImmutableList.CreateRange(hookDictionary.Values);
            }
            catch (Exception)
            {
                _currentProvider = null;
                _hooks = ImmutableList<HookModel>.Empty;
            }
        }

        /// <summary>
        /// Get current version of game
        /// </summary>
        public static string? CurrentVersion => _currentProvider?.Version;

        /// <summary>
        /// Gets all configured hook signatures.
        /// </summary>
        public static ImmutableList<HookModel> HookSignatures => _hooks;

        /// <summary>
        /// Checks if a given method name or signature is a known hook.
        /// This method supports both full signatures and just method names.
        /// </summary>
        public static bool IsHook(IMethodSymbol method)
        {
            if (method == null || method.ContainingType == null ||
                !HooksUtils.IsRustClass(method.ContainingType))
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null) return false;

            // Находим все хуки с таким же именем
            var matchingHooks = _hooks.Where(s => s.HookName == methodSignature.HookName).ToList();

            foreach (var hook in matchingHooks)
            {
                // Проверяем количество параметров
                if (hook.HookParameters.Count != method.Parameters.Length)
                    continue;

                bool allParametersMatch = true;
                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    var methodParam = method.Parameters[i].Type;
                    var hookParamName = hook.HookParameters[i];

                    // Проверяем соответствие типов
                    if (!HooksUtils.IsTypeCompatible(methodParam, hookParamName.Type))
                    {
                        allParametersMatch = false;
                        break;
                    }
                }

                if (allParametersMatch)
                    return true;
            }
            return false;
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
        /// Returns hooks with similar names to the method.
        /// </summary>
        public static IEnumerable<string> GetSimilarHooks(IMethodSymbol method, int maxSuggestions = 3)
        {
            if (method == null || method.ContainingType == null ||
                !HooksUtils.IsRustClass(method.ContainingType))
                return Enumerable.Empty<string>();

            var candidates = _hooks.Select(h => (text: h.HookName, context: string.Empty));
            return StringSimilarity.FindSimilarWithContext(
                method.Name,
                candidates,
                maxSuggestions)
                .Select(r => r.Text);
        }
    }
}
