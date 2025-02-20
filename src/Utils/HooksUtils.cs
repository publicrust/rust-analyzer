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

        public static MethodSignatureModel GetMethodSignature(IMethodSymbol method)
        {
            if (method == null)
                return null;

            var parameters = method
                .Parameters.Select(p => new MethodParameter
                {
                    Type = GetFriendlyTypeName(p.Type),
                    Name = p.Name,
                })
                .ToList();

            return new MethodSignatureModel { Name = method.Name, Parameters = parameters };
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

        public static MethodSignatureModel ParseHookString(string hookString)
        {
            if (string.IsNullOrWhiteSpace(hookString))
            {
                return null;
            }

            // Поиск возвращаемого типа (если есть)
            string returnType = "void";
            string methodPart = hookString;
            
            var parts = hookString.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && !parts[0].Contains("("))
            {
                returnType = parts[0];
                methodPart = parts[1];
            }

            // Извлечение имени метода и параметров
            var openParenIndex = methodPart.IndexOf('(');
            var closeParenIndex = methodPart.LastIndexOf(')');

            if (openParenIndex < 0 || closeParenIndex < 0 || closeParenIndex <= openParenIndex)
            {
                throw new FormatException($"Invalid hook format: {hookString}");
            }

            var hookName = methodPart.Substring(0, openParenIndex).Trim();
            var parameters = methodPart.Substring(
                openParenIndex + 1,
                closeParenIndex - openParenIndex - 1
            );

            // Парсинг параметров с поддержкой опциональных параметров
            var parameterList = new List<MethodParameter>();
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                var currentParam = new StringBuilder();
                var bracketCount = 0;
                
                for (int i = 0; i < parameters.Length; i++)
                {
                    char c = parameters[i];
                    if (c == '<') bracketCount++;
                    else if (c == '>') bracketCount--;
                    else if (c == ',' && bracketCount == 0)
                    {
                        ParseAndAddParameter(currentParam.ToString(), parameterList);
                        currentParam.Clear();
                        continue;
                    }
                    currentParam.Append(c);
                }
                
                if (currentParam.Length > 0)
                {
                    ParseAndAddParameter(currentParam.ToString(), parameterList);
                }
            }

            // Создаем базовую или расширенную модель в зависимости от наличия дополнительной информации
            if (returnType == "void" && !parameterList.Any(p => p is PluginMethodParameter))
            {
                return new MethodSignatureModel { Name = hookName, Parameters = parameterList };
            }
            
            return new PluginMethod 
            { 
                Name = hookName, 
                Parameters = parameterList,
                ReturnType = returnType
            };
        }

        private static void ParseAndAddParameter(string paramStr, List<MethodParameter> parameters)
        {
            var param = paramStr.Trim();
            if (string.IsNullOrEmpty(param)) return;

            var parts = param.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Проверяем наличие значения по умолчанию
            string defaultValue = null;
            bool isOptional = false;
            
            var equalsIndex = param.IndexOf('=');
            if (equalsIndex != -1)
            {
                defaultValue = param.Substring(equalsIndex + 1).Trim();
                param = param.Substring(0, equalsIndex).Trim();
                isOptional = true;
                parts = param.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }

            if (parts.Length == 1)
            {
                parameters.Add(new MethodParameter { Type = parts[0] });
            }
            else
            {
                if (isOptional)
                {
                    parameters.Add(new PluginMethodParameter 
                    { 
                        Type = parts[0],
                        Name = parts[1],
                        IsOptional = true,
                        DefaultValue = defaultValue
                    });
                }
                else
                {
                    parameters.Add(new MethodParameter 
                    { 
                        Type = parts[0],
                        Name = parts[1]
                    });
                }
            }
        }
    }
}
