using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CommandStructureAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST007";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Invalid command structure";
        private static readonly LocalizableString MessageFormat = "{0}";
        private static readonly LocalizableString Description = "Commands must follow the correct parameter structure based on their type.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        private static readonly Dictionary<string, CommandStructure> CommandStructures = new Dictionary<string, CommandStructure>
        {
            {
                "ChatCommand",
                new CommandStructure(
                    "ChatCommand",
                    new[] { "BasePlayer", "string", "string[]" },
                    new[] { "player", "command", "args" },
                    "[ChatCommand(\"name\")]\nvoid CommandName(BasePlayer player, string command, string[] args)")
            },
            {
                "Command",
                new CommandStructure(
                    "Command",
                    new[] { "IPlayer", "string", "string[]" },
                    new[] { "player", "command", "args" },
                    "[Command(\"name\")]\nvoid CommandName(IPlayer player, string command, string[] args)")
            },
            {
                "ConsoleCommand",
                new CommandStructure(
                    "ConsoleCommand",
                    new[] { "ConsoleSystem.Arg" },
                    new[] { "args" },
                    "[ConsoleCommand(\"name\")]\nvoid CommandName(ConsoleSystem.Arg args)")
            }
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
                return;

            Console.WriteLine($"[RustAnalyzer] Analyzing method: {methodSymbol.Name}");

            // Получаем атрибуты команд
            var commandAttributes = methodSymbol.GetAttributes()
                .Where(attr => 
                {
                    if (attr.AttributeClass == null) return false;
                    var attrName = attr.AttributeClass.Name;
                    var attrFullName = attr.AttributeClass.ToDisplayString();
                    
                    Console.WriteLine($"[RustAnalyzer] Found attribute: {attrName} ({attrFullName})");
                    
                    return CommandStructures.Keys.Any(ca => 
                        attrName.Equals(ca, StringComparison.OrdinalIgnoreCase) ||
                        attrName.Equals(ca + "Attribute", StringComparison.OrdinalIgnoreCase) ||
                        attrFullName.EndsWith($".{ca}", StringComparison.OrdinalIgnoreCase) ||
                        attrFullName.EndsWith($".{ca}Attribute", StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            Console.WriteLine($"[RustAnalyzer] Found command attributes: {string.Join(", ", commandAttributes.Select(a => a.AttributeClass?.Name))}");

            // Если есть атрибуты команд, проверяем их структуру
            if (commandAttributes.Any())
            {
                foreach (var attribute in commandAttributes)
                {
                    var attributeName = attribute.AttributeClass?.Name;
                    if (attributeName == null) continue;

                    // Убираем суффикс Attribute для поиска в словаре
                    var structureName = attributeName.EndsWith("Attribute") 
                        ? attributeName.Substring(0, attributeName.Length - "Attribute".Length)
                        : attributeName;

                    Console.WriteLine($"[RustAnalyzer] Checking attribute: {attributeName} (structure: {structureName})");
                    var structure = CommandStructures[structureName];

                    // Проверяем, что атрибут имеет конструктор с параметром name
                    if (attribute.ConstructorArguments.Length == 0)
                    {
                        ReportDiagnostic(context, methodDeclaration,
                            $"The {attributeName} attribute must have a name parameter. Example:\n{structure.Example}");
                        continue;
                    }

                    // Проверяем параметры метода
                    if (!IsValidParameterStructure(methodSymbol, structure))
                    {
                        Console.WriteLine($"[RustAnalyzer] Invalid parameter structure for {attributeName}");
                        ReportDiagnostic(context, methodDeclaration,
                            $"Invalid parameter structure for {attributeName}. Expected:\n{structure.Example}");
                    }
                }
            }
            // Если метод похож на команду по имени
            else if (CommandUtils.IsCommand(methodSymbol))
            {
                Console.WriteLine($"[RustAnalyzer] Method looks like a command by name");
                // Проверяем, соответствует ли метод хотя бы одной из известных структур
                var matchingStructures = CommandStructures.Values
                    .Where(structure => IsValidParameterStructure(methodSymbol, structure))
                    .ToList();

                Console.WriteLine($"[RustAnalyzer] Matching structures: {string.Join(", ", matchingStructures.Select(s => s.AttributeName))}");

                if (!matchingStructures.Any())
                {
                    // Если метод не соответствует ни одной структуре, показываем все возможные варианты
                    var message = "This method appears to be a command but has invalid parameter structure. Valid command structures are:\n\n" +
                        string.Join("\n\n", CommandStructures.Values.Select(s => s.Example));
                    ReportDiagnostic(context, methodDeclaration, message);
                }
            }
        }

        private bool IsValidParameterStructure(IMethodSymbol method, CommandStructure structure)
        {
            Console.WriteLine($"[RustAnalyzer] Checking parameters for {method.Name} against {structure.AttributeName}");
            Console.WriteLine($"[RustAnalyzer] Method parameters: {string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))}");
            Console.WriteLine($"[RustAnalyzer] Expected parameters: {string.Join(", ", structure.ParameterTypes.Zip(structure.ParameterNames, (t, n) => $"{t} {n}"))}");

            if (method.Parameters.Length != structure.ParameterTypes.Length)
            {
                Console.WriteLine($"[RustAnalyzer] Parameter count mismatch: {method.Parameters.Length} vs {structure.ParameterTypes.Length}");
                return false;
            }

            for (int i = 0; i < method.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                var expectedType = structure.ParameterTypes[i];

                Console.WriteLine($"[RustAnalyzer] Checking parameter {i}: {parameter.Type.ToDisplayString()} vs {expectedType}");

                // Проверяем тип параметра
                if (!IsMatchingType(parameter.Type, expectedType))
                {
                    Console.WriteLine($"[RustAnalyzer] Type mismatch for parameter {i}");
                    return false;
                }

                // Имена параметров проверяем только если есть атрибут
                if (HasCommandAttribute(method, structure.AttributeName))
                {
                    var expectedName = structure.ParameterNames[i];
                    Console.WriteLine($"[RustAnalyzer] Checking parameter name: {parameter.Name} vs {expectedName}");
                    if (parameter.Name != expectedName)
                    {
                        Console.WriteLine($"[RustAnalyzer] Name mismatch for parameter {i}");
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsMatchingType(ITypeSymbol actualType, string expectedType)
        {
            var actualTypeName = actualType.ToDisplayString();
            Console.WriteLine($"[RustAnalyzer] Comparing types: {actualTypeName} vs {expectedType}");

            // Обрабатываем специальные случаи
            if (expectedType == "ConsoleSystem.Arg" && actualTypeName.EndsWith("ConsoleSystem.Arg"))
                return true;

            // Проверяем точное совпадение или полное имя типа
            var result = actualTypeName == expectedType || 
                   actualTypeName == $"global::{expectedType}" ||
                   actualTypeName == $"Oxide.Game.Rust.Libraries.{expectedType}" ||
                   actualTypeName == $"Oxide.Core.Libraries.{expectedType}";

            Console.WriteLine($"[RustAnalyzer] Type match result: {result}");
            return result;
        }

        private bool HasCommandAttribute(IMethodSymbol method, string attributeName)
        {
            return method.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name.Equals(attributeName, StringComparison.OrdinalIgnoreCase) == true);
        }

        private void ReportDiagnostic(SyntaxNodeAnalysisContext context, SyntaxNode node, string message)
        {
            var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), message);
            context.ReportDiagnostic(diagnostic);
        }

        private class CommandStructure
        {
            public string AttributeName { get; }
            public string[] ParameterTypes { get; }
            public string[] ParameterNames { get; }
            public string Example { get; }

            public CommandStructure(string attributeName, string[] parameterTypes, string[] parameterNames, string example)
            {
                AttributeName = attributeName;
                ParameterTypes = parameterTypes;
                ParameterNames = parameterNames;
                Example = example;
            }
        }
    }
} 