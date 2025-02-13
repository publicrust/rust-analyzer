using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using RustAnalyzer.Configuration;
using RustAnalyzer.Models;
using RustAnalyzer.src.Hooks.Interfaces;
using RustAnalyzer.src.Hooks.Providers;
using RustAnalyzer.src.Services;
using RustAnalyzer.Utils;

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

                var allHooks = new List<HookModel>();
                var processedHookSignatures = new HashSet<string>();

                foreach (var hook in regularHooks)
                {
                    allHooks.Add(hook);
                    var signature =
                        $"{hook.HookName}:{string.Join(",", hook.HookParameters.Select(p => p.Type))}";
                    processedHookSignatures.Add(signature);
                }

                foreach (var hook in providerHooks)
                {
                    var signature =
                        $"{hook.HookName}:{string.Join(",", hook.HookParameters.Select(p => p.Type))}";
                    if (!processedHookSignatures.Contains(signature))
                    {
                        allHooks.Add(hook);
                        processedHookSignatures.Add(signature);
                    }
                }

                _currentProvider = provider;
                _hooks = ImmutableList.CreateRange(allHooks);
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
            if (
                method == null
                || method.ContainingType == null
                || !HooksUtils.IsRustClass(method.ContainingType)
            )
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

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
            if (
                method == null
                || method.ContainingType == null
                || !HooksUtils.IsRustClass(method.ContainingType)
            )
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            return _hooks.Any(s => s.HookName == methodSignature.HookName);
        }

        /// <summary>
        /// Returns hooks with similar names to the method.
        /// </summary>
        public static IEnumerable<HookModel> GetSimilarHooks(
            IMethodSymbol method,
            int maxSuggestions = 3
        )
        {
            if (method == null)
                return Enumerable.Empty<HookModel>();

            if (!_hooks.Any())
                return Enumerable.Empty<HookModel>();

            // Включаем полную сигнатуру в текст для сравнения
            var candidates = _hooks.Select(h =>
                (
                    text: $"{h.HookName}({string.Join(", ", h.HookParameters.Select(p => p.Type))})",
                    context: h
                )
            );

            return StringSimilarity
                .FindSimilarWithContext(method.Name, candidates, maxSuggestions)
                .Select(r => r.Context);
        }
    }
}
