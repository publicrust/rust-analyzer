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
                CommandAttributes.Any(ca => 
                    attr.AttributeClass?.Name.Equals(ca, StringComparison.OrdinalIgnoreCase) == true)))
            {
                return true;
            }

            // Проверяем имя метода на наличие индикаторов команды
            var methodNameLower = method.Name.ToLowerInvariant();
            return CommandNameIndicators.Any(indicator => 
                methodNameLower.Contains(indicator, StringComparison.OrdinalIgnoreCase));
        }
    }
} 