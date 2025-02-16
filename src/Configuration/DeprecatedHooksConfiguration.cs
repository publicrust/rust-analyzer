using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RustAnalyzer.Models;
using RustAnalyzer.src.DeprecatedHooks.Interfaces;
using RustAnalyzer.Utils;

namespace RustAnalyzer.src.Configuration
{
    public static class DeprecatedHooksConfiguration
    {
        private static IDeprecatedHooksProvider _provider = null!;
        private static List<DeprecatedHookModel> _hooks = new();

        /// <summary>
        /// Initializes the hooks configuration with the specified provider
        /// </summary>
        public static void Initialize(IDeprecatedHooksProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            try
            {
                _provider = provider;
                _hooks = provider.GetHooks();
            }
            catch (Exception)
            {
                _provider = null;
                _hooks = new List<DeprecatedHookModel>();
            }
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
                if (hook.OldHook.HookName != methodSignature.HookName)
                    continue;

                // Проверяем количество параметров
                if (hook.OldHook.HookParameters.Count != methodSignature.HookParameters.Count)
                    continue;

                // Проверяем типы параметров
                bool allParametersMatch = true;
                for (int i = 0; i < methodSignature.HookParameters.Count; i++)
                {
                    if (
                        hook.OldHook.HookParameters[i].Type
                        != methodSignature.HookParameters[i].Type
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
