using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using RustAnalyzer.Models;

namespace RustAnalyzer.Utils
{
    internal static class HooksUtils
    {
        public static readonly Dictionary<SpecialType, string> SpecialTypeMap = new Dictionary<
            SpecialType,
            string
        >
        {
            { SpecialType.System_Object, "object" },
            { SpecialType.System_Boolean, "bool" },
            { SpecialType.System_Char, "char" },
            { SpecialType.System_SByte, "sbyte" },
            { SpecialType.System_Byte, "byte" },
            { SpecialType.System_Int16, "short" },
            { SpecialType.System_UInt16, "ushort" },
            { SpecialType.System_Int32, "int" },
            { SpecialType.System_UInt32, "uint" },
            { SpecialType.System_Int64, "long" },
            { SpecialType.System_UInt64, "ulong" },
            { SpecialType.System_Decimal, "decimal" },
            { SpecialType.System_Single, "float" },
            { SpecialType.System_Double, "double" },
            { SpecialType.System_String, "string" },
        };

        public static bool IsRustClass(INamedTypeSymbol typeSymbol)
        {
            while (typeSymbol != null)
            {
                var currentName = typeSymbol.ToDisplayString();
                if (
                    currentName == "Oxide.Core.Plugins.Plugin"
                    || currentName == "Oxide.Plugins.RustPlugin"
                    || currentName == "Oxide.Plugins.CovalencePlugin"
                )
                {
                    return true;
                }
                typeSymbol = typeSymbol.BaseType;
            }
            return false;
        }

        public static bool IsUnityClass(INamedTypeSymbol typeSymbol)
        {
            while (typeSymbol != null)
            {
                if (typeSymbol.ToDisplayString() == "UnityEngine.MonoBehaviour")
                    return true;
                typeSymbol = typeSymbol.BaseType;
            }
            return false;
        }

        public static bool IsTypeCompatible(ITypeSymbol type, string expectedTypeName)
        {
            if (type.Name == expectedTypeName || type.ToDisplayString() == expectedTypeName)
                return true;

            var currentType = type;
            while (currentType.BaseType != null)
            {
                currentType = currentType.BaseType;
                if (
                    currentType.Name == expectedTypeName
                    || currentType.ToDisplayString() == expectedTypeName
                )
                    return true;
            }

            foreach (var iface in type.AllInterfaces)
            {
                if (iface.Name == expectedTypeName || iface.ToDisplayString() == expectedTypeName)
                    return true;
            }

            return false;
        }

        public static HookModel GetMethodSignature(IMethodSymbol method)
        {
            if (method == null)
                return null;

            var parameters = method
                .Parameters.Select(p => new HookParameter
                {
                    Type = GetFriendlyTypeName(p.Type),
                    Name = p.Name,
                })
                .ToList();

            return new HookModel { HookName = method.Name, HookParameters = parameters };
        }

        public static string GetFriendlyTypeName(ITypeSymbol type)
        {
            if (type == null)
                return null;

            if (SpecialTypeMap.TryGetValue(type.SpecialType, out var friendlyName))
                return friendlyName;

            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var genericTypeName = namedType.ConstructedFrom.Name.Split('`')[0];
                var genericArguments = namedType.TypeArguments.Select(GetFriendlyTypeName);
                return $"{genericTypeName}<{string.Join(", ", genericArguments)}>";
            }

            if (type is IArrayTypeSymbol arrayType)
            {
                var elementType = GetFriendlyTypeName(arrayType.ElementType);
                return $"{elementType}[]";
            }

            return type.ToDisplayString(
                new SymbolDisplayFormat(
                    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                    genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
                )
            );
        }

        public static HookModel ParseHookString(string hookString)
        {
            if (string.IsNullOrWhiteSpace(hookString))
            {
                return null;
            }

            // Extracting the hook name and parameters
            var openParenIndex = hookString.IndexOf('(');
            var closeParenIndex = hookString.LastIndexOf(')');

            if (openParenIndex < 0 || closeParenIndex < 0 || closeParenIndex <= openParenIndex)
            {
                throw new FormatException($"Invalid hook format: {hookString}");
            }

            var hookName = hookString.Substring(0, openParenIndex).Trim();
            var parameters = hookString.Substring(
                openParenIndex + 1,
                closeParenIndex - openParenIndex - 1
            );

            // Split parameters and handle both formats
            var parameterList = parameters
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p =>
                {
                    var parts = p.Trim()
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // Handle type-only format
                    if (parts.Length == 1)
                    {
                        return new HookParameter { Type = parts[0] };
                    }

                    // Handle generic types with parameter names
                    if (parts[1].Contains("<"))
                    {
                        return new HookParameter { Type = parts[0] };
                    }

                    // Handle type with parameter name
                    return new HookParameter { Type = parts[0], Name = parts[1] };
                })
                .ToList();

            return new HookModel { HookName = hookName, HookParameters = parameterList };
        }
    }
}
