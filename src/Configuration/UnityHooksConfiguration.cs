using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using RustAnalyzer.Models;
using RustAnalyzer.Utils;

namespace RustAnalyzer
{
    /// <summary>
    /// Contains Unity-specific hook signatures and logic to identify them.
    /// </summary>
    public static class UnityHooksConfiguration
    {
        private static readonly ImmutableList<MethodSignatureModel> _hooks;

        static UnityHooksConfiguration()
        {
            try
            {
                var hooks = UnityHooksJson.GetHooks();
                _hooks = ImmutableList.CreateRange(hooks);
            }
            catch (Exception ex)
            {
                _hooks = ImmutableList<MethodSignatureModel>.Empty;
            }
        }

        /// <summary>
        /// Gets all configured hook signatures.
        /// </summary>
        public static ImmutableList<MethodSignatureModel> HookSignatures => _hooks;

        /// <summary>
        /// Checks if a given method name or signature is a known hook.
        /// This method supports both full signatures and just method names.
        /// </summary>
        public static bool IsHook(IMethodSymbol method)
        {
            if (
                method == null
                || method.ContainingType == null
                || !HooksUtils.IsUnityClass(method.ContainingType)
            )
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            // ��������� ������� ���� � ����� ������
            return _hooks.Any(s => s.Name == methodSignature.Name);
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
                || !HooksUtils.IsUnityClass(method.ContainingType)
            )
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            return _hooks.Any(s => s.Name == methodSignature.Name);
        }

        /// <summary>
        /// Returns hooks with similar names to the method.
        /// </summary>
        public static IEnumerable<MethodSignatureModel> GetSimilarHooks(
            IMethodSymbol method,
            int maxSuggestions = 3
        )
        {
            if (
                method == null
                || method.ContainingType == null
                || !HooksUtils.IsUnityClass(method.ContainingType)
            )
                return Enumerable.Empty<MethodSignatureModel>();

            var candidates = _hooks.Select(h => (text: h.Name, context: h));
            return StringSimilarity
                .FindSimilarWithContext(method.Name, candidates, maxSuggestions)
                .Select(r => r.Context);
        }
    }
}
