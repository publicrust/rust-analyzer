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
    /// Contains Rust-specific hook signatures and logic to identify them.
    /// </summary>
    public static class HooksConfiguration
    {
        private static ImmutableList<HookModel> _hooks = ImmutableList<HookModel>.Empty;

        /// <summary>
        /// Initializes the hooks configuration with the specified provider
        /// </summary>
        public static void Initialize(List<HookModel> hooks)
        {
            if (hooks == null)
            {
                throw new ArgumentNullException(nameof(hooks));
            }

            _hooks = ImmutableList.CreateRange(hooks);
            Console.WriteLine($"[RustAnalyzer] Loaded {hooks.Count} hooks");
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
            if (method == null || !HooksUtils.IsRustClass(method.ContainingType))
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            return _hooks.Any(s => 
                s.Signature.Name == methodSignature.Name &&
                s.Signature.Parameters.Count == methodSignature.Parameters.Count &&
                s.Signature.Parameters.Select(p => p.Type)
                    .SequenceEqual(methodSignature.Parameters.Select(p => p.Type))
            );
        }

        /// <summary>
        /// Checks if a given method signature exactly matches a known hook signature.
        /// This method requires the full signature to match.
        /// </summary>
        public static bool IsKnownHook(IMethodSymbol method)
        {
            if (method == null || !HooksUtils.IsRustClass(method.ContainingType))
                return false;

            var methodSignature = HooksUtils.GetMethodSignature(method);
            if (methodSignature == null)
                return false;

            return _hooks.Any(s =>
                s.Signature.Name == methodSignature.Name
                && s.Signature.Parameters.Count == methodSignature.Parameters.Count
                && s.Signature.Parameters.Select(p => p.Type)
                    .SequenceEqual(methodSignature.Parameters.Select(p => p.Type))
            );
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
                    text: $"{h.Signature.Name}({string.Join(", ", h.Signature.Parameters.Select(p => p.Type))})",
                    context: h
                )
            );

            return StringSimilarity
                .FindSimilarWithContext(method.Name, candidates, maxSuggestions)
                .Select(r => r.Context);
        }
    }
}
