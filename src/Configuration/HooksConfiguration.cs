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

        // mapping C# aliases to CLR names
        private static readonly Dictionary<string, string> _typeAliases = new(StringComparer.Ordinal)
        {
            ["bool"]    = "Boolean",
            ["byte"]    = "Byte",
            ["sbyte"]   = "SByte",
            ["char"]    = "Char",
            ["decimal"] = "Decimal",
            ["double"]  = "Double",
            ["float"]   = "Single",
            ["int"]     = "Int32",
            ["uint"]    = "UInt32",
            ["long"]    = "Int64",
            ["ulong"]   = "UInt64",
            ["object"]  = "Object",
            ["short"]   = "Int16",
            ["ushort"]  = "UInt16",
            ["string"]  = "String"
        };

        /// <summary>
        /// Initializes the hooks configuration with the specified provider.
        /// </summary>
        public static void Initialize(List<HookModel> hooks)
        {
            if (hooks == null)
                throw new ArgumentNullException(nameof(hooks));

            _hooks = ImmutableList.CreateRange(hooks);
            Console.WriteLine($"[RustAnalyzer] Loaded {hooks.Count} hooks");
        }

        /// <summary>
        /// All configured hook signatures.
        /// </summary>
        public static ImmutableList<HookModel> HookSignatures => _hooks;

        /// <summary>
        /// Checks if the method matches a known hook:
        /// name, parameter count, and type compatibility (inheritance & C# aliases).
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

            return sig.Parameters
                .Select((p, i) => new { Expected = p.Type, Actual = method.Parameters[i].Type })
                .All(x => IsTypeCompatible(x.Actual, x.Expected, method));
        }

        /// <summary>
        /// Name-only check.
        /// </summary>
        public static bool IsKnownHook(IMethodSymbol method)
        {
            if (method == null || !HooksUtils.IsRustClass(method.ContainingType))
                return false;

            var sig = HooksUtils.GetMethodSignature(method);
            return sig != null && _hooks.Any(h => h.Signature.Name == sig.Name);
        }

        /// <summary>
        /// Finds similar hooks by name.
        /// </summary>
        public static IEnumerable<HookModel> GetSimilarHooks(IMethodSymbol method, int maxSuggestions = 3)
        {
            if (method == null || !_hooks.Any())
                return Enumerable.Empty<HookModel>();

            var candidates = _hooks.Select(h =>
                (
                    text: $"{h.Signature.Name}({string.Join(", ", h.Signature.Parameters.Select(p => p.Type))})",
                    context: h
                ));

            return StringSimilarity
                .FindSimilarWithContext(method.Name, candidates, maxSuggestions)
                .Select(r => r.Context);
        }

        // -------------- Helpers --------------

        /// <summary>
        /// Compatibility between actualType and expectedName string:
        /// inheritance in either direction + accounting for C# aliases.
        /// </summary>
        private static bool IsTypeCompatible(ITypeSymbol actualType, string expectedName, IMethodSymbol context)
        {
            var normExpected = NormalizeTypeName(expectedName);
            var normActual   = NormalizeTypeName(actualType.Name);

            // actual → expected
            if (IsOrDerivesFrom(actualType, normExpected))
                return true;

            // expected → actual
            var expectedSym = FindTypeByName(normExpected, context);
            if (expectedSym != null && IsOrDerivesFrom(expectedSym, normActual))
                return true;

            return false;
        }

        /// <summary>
        /// Normalizes the name: removes '?' and replaces aliases with CLR names.
        /// </summary>
        private static string NormalizeTypeName(string name)
        {
            if (name.EndsWith("?", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - 1);

            if (_typeAliases.TryGetValue(name, out var clr))
                return clr;

            return name;
        }

        /// <summary>
        /// Checks if the type or any of its BaseTypes has the name == name.
        /// </summary>
        private static bool IsOrDerivesFrom(ITypeSymbol type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (string.Equals(t.Name, name, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Finds a type by its simple name, returns null if not found.
        /// </summary>
        private static ITypeSymbol? FindTypeByName(string typeName, IMethodSymbol method)
        {
            var visited = new HashSet<INamespaceSymbol>(SymbolEqualityComparer.Default);
            var queue   = new Queue<INamespaceSymbol>();
            queue.Enqueue(method.ContainingAssembly.GlobalNamespace);

            while (queue.Count > 0)
            {
                var ns = queue.Dequeue();
                if (!visited.Add(ns)) continue;

                foreach (var t in ns.GetTypeMembers())
                {
                    if (t.Name == typeName)
                        return t;
                }
                foreach (var child in ns.GetNamespaceMembers())
                {
                    queue.Enqueue(child);
                }
            }

            // references
            foreach (var module in method.ContainingAssembly.Modules)
            {
                foreach (var asm in module.ReferencedAssemblySymbols)
                {
                    queue.Enqueue(asm.GlobalNamespace);
                    while (queue.Count > 0)
                    {
                        var ns = queue.Dequeue();
                        if (!visited.Add(ns)) continue;

                        foreach (var t in ns.GetTypeMembers())
                        {
                            if (t.Name == typeName)
                                return t;
                        }
                        foreach (var child in ns.GetNamespaceMembers())
                        {
                            queue.Enqueue(child);
                        }
                    }
                }
            }

            return null;
        }
    }
}
