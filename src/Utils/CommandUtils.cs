using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace RustAnalyzer.Utils
{
    /// <summary>
    /// Утилиты для работы с командами Rust
    /// </summary>
    public static class CommandUtils
    {
        private static readonly string[] CommandAttributes = new[]
        {
            "ChatCommand",
            "Command",
            "ConsoleCommand"
        };

        private static readonly string[] CommandNameIndicators = new[]
        {
            "command",
            "cmd"
        };

        /// <summary>
        /// Проверяет, является ли метод командой на основе:
        /// 1. Наличия атрибутов [ChatCommand], [Command], [ConsoleCommand]
        /// 2. Наличия слов "command" или "cmd" в имени метода
        /// </summary>
        public static bool IsCommand(IMethodSymbol method)
        {
            if (method == null)
                return false;

            // Проверяем наличие атрибутов команд
            if (method.GetAttributes().Any(attr => 
            {
                if (attr.AttributeClass == null) return false;
                var attrName = attr.AttributeClass.Name;
                var attrFullName = attr.AttributeClass.ToDisplayString();

                return CommandAttributes.Any(ca => 
                    attrName.Equals(ca, StringComparison.OrdinalIgnoreCase) ||
                    attrName.Equals(ca + "Attribute", StringComparison.OrdinalIgnoreCase) ||
                    attrFullName.EndsWith($".{ca}", StringComparison.OrdinalIgnoreCase) ||
                    attrFullName.EndsWith($".{ca}Attribute", StringComparison.OrdinalIgnoreCase));
            }))
            {
                return true;
            }

            // Проверяем имя метода на наличие индикаторов команды
            var methodNameLower = method.Name.ToLowerInvariant();
            return CommandNameIndicators.Any(indicator => 
                methodNameLower.IndexOf(indicator, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
} 