using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Configuration;
using RustAnalyzer.Utils;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CommandStructureAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST007";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Invalid command structure";
        private static readonly LocalizableString MessageFormat = "{0}";
        private static readonly LocalizableString Description =
            "Commands must follow the correct parameter structure based on their type.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

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
            var commandAttributes = methodSymbol
                .GetAttributes()
                .Where(attr =>
                {
                    if (attr.AttributeClass == null)
                        return false;
                    var attrName = attr.AttributeClass.Name;
                    var attrFullName = attr.AttributeClass.ToDisplayString();

                    return CommandStructureInfo.CommandStructures.Keys.Any(ca =>
                        attrName.Equals(ca, StringComparison.OrdinalIgnoreCase)
                        || attrName.Equals(ca + "Attribute", StringComparison.OrdinalIgnoreCase)
                        || attrFullName.EndsWith($".{ca}", StringComparison.OrdinalIgnoreCase)
                        || attrFullName.EndsWith(
                            $".{ca}Attribute",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
                })
                .ToList();

            // Если есть атрибуты команд, проверяем их структуру
            if (commandAttributes.Any())
            {
                foreach (var attribute in commandAttributes)
                {
                    var attributeName = attribute.AttributeClass?.Name;
                    if (attributeName == null)
                        continue;

                    // Убираем суффикс Attribute для поиска в словаре
                    var structureName = attributeName.EndsWith("Attribute")
                        ? attributeName.Substring(0, attributeName.Length - "Attribute".Length)
                        : attributeName;

                    var structure = CommandStructureInfo.CommandStructures[structureName];

                    // Проверяем, что атрибут имеет конструктор с параметром name
                    if (attribute.ConstructorArguments.Length == 0)
                    {
                        ReportDiagnostic(
                            context,
                            methodDeclaration,
                            $"The {attributeName} attribute must have a name parameter. Example:\n{structure.Example}"
                        );
                        continue;
                    }

                    // Проверяем параметры метода
                    if (!IsValidParameterStructure(methodSymbol, structure))
                    {
                        ReportDiagnostic(
                            context,
                            methodDeclaration,
                            $"Invalid parameter structure for {attributeName}. Expected:\n{structure.Example}"
                        );
                    }
                }
            }
            // Если метод похож на команду по имени
            else if (
                CommandUtils.IsCommand(methodSymbol) && !HooksConfiguration.IsHook(methodSymbol)
            )
            {
                // Проверяем, соответствует ли метод хотя бы одной из известных структур
                var matchingStructures = CommandStructureInfo
                    .CommandStructures.Values.Where(structure =>
                        IsValidParameterStructure(methodSymbol, structure)
                    )
                    .ToList();

                if (!matchingStructures.Any())
                {
                    // Если метод не соответствует ни одной структуре, показываем все возможные варианты
                    var message =
                        "This method appears to be a command but has invalid parameter structure. Valid command structures are:\n\n"
                        + string.Join(
                            "\n\n",
                            CommandStructureInfo.CommandStructures.Values.Select(s => s.Example)
                        );
                    ReportDiagnostic(context, methodDeclaration, message);
                }
            }
        }

        private bool IsValidParameterStructure(IMethodSymbol method, CommandStructure structure)
        {
            if (method.Parameters.Length != structure.ParameterTypes.Length)
            {
                return false;
            }

            for (int i = 0; i < method.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                var expectedType = structure.ParameterTypes[i];

                // Проверяем тип параметра
                if (!IsMatchingType(parameter.Type, expectedType))
                {
                    return false;
                }

                // Имена параметров проверяем только если есть атрибут
                if (HasCommandAttribute(method, structure.AttributeName))
                {
                    var expectedName = structure.ParameterNames[i];
                    if (parameter.Name != expectedName)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsMatchingType(ITypeSymbol actualType, string expectedType)
        {
            var actualTypeName = actualType.ToDisplayString();

            // Обрабатываем специальные случаи
            if (expectedType == "ConsoleSystem.Arg" && actualTypeName.EndsWith("ConsoleSystem.Arg"))
                return true;

            // Проверяем точное совпадение или полное имя типа
            var result =
                actualTypeName == expectedType
                || actualTypeName == $"global::{expectedType}"
                || actualTypeName == $"Oxide.Game.Rust.Libraries.{expectedType}"
                || actualTypeName == $"Oxide.Core.Libraries.{expectedType}";

            return result;
        }

        private bool HasCommandAttribute(IMethodSymbol method, string attributeName)
        {
            return method
                .GetAttributes()
                .Any(attr =>
                    attr.AttributeClass?.Name.Equals(
                        attributeName,
                        StringComparison.OrdinalIgnoreCase
                    ) == true
                );
        }

        private void ReportDiagnostic(
            SyntaxNodeAnalysisContext context,
            SyntaxNode node,
            string message
        )
        {
            var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), message);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
