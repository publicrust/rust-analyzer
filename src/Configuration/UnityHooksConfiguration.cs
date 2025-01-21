using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using RustAnalyzer.Models;
using RustAnalyzer.Utils;

namespace RustAnalyzer
{
    /// <summary>
    /// Contains Unity-specific hook signatures and logic to identify them.
    /// </summary>
    public static class UnityHooksConfiguration
    {
        private static readonly ImmutableList<HookModel> _hooks;

        static UnityHooksConfiguration()
        {
            try
            {
                var hooks = UnityHooksJson.GetHooks();
                _hooks = ImmutableList.CreateRange(hooks);
            }
            catch (Exception ex)
            {
                _hooks = ImmutableList<HookModel>.Empty;
            }
        }

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
                !HooksUtils.IsUnityClass(method.ContainingType))
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null) return false;

            // ��������� ������� ���� � ����� ������
            return _hooks.Any(s => s.HookName == methodSignature.HookName);
        }

        /// <summary>
        /// Checks if a given method signature exactly matches a known hook signature.
        /// This method requires the full signature to match.
        /// </summary>
        public static bool IsKnownHook(IMethodSymbol method)
        {
            if (method == null || method.ContainingType == null ||
                !HooksUtils.IsUnityClass(method.ContainingType))
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            return _hooks.Any(s => s.HookName == methodSignature.HookName);
        }

        /// <summary>
        /// Returns hooks with similar names to the method.
        /// </summary>
        public static IEnumerable<HookModel> GetSimilarHooks(IMethodSymbol method, int maxSuggestions = 3)
        {
            if (method == null || method.ContainingType == null ||
                !HooksUtils.IsUnityClass(method.ContainingType))
                return Enumerable.Empty<HookModel>();

            var candidates = _hooks.Select(h => (text: h.HookName, context: h));
            return StringSimilarity.FindSimilarWithContext(method.Name, candidates, maxSuggestions)
                .Select(r => r.Context);
        }
    }
}
