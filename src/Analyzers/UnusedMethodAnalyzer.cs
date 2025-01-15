using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Collections.Generic;
using RustAnalyzer.Utils;

namespace RustAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnusedMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST003";
        private const string Category = "Design";

        private static readonly LocalizableString Title = "Unused method detected";
        private static readonly LocalizableString Description = "Methods should be used or removed to maintain clean code.";

        private static readonly LocalizableString MessageFormat = "Method '{0}' is never used";
        private static readonly LocalizableString MessageFormatWithHooks = 
            "Method '{0}' is never used.\n" +
            "If you intended this to be a hook, no matching hook was found.\n" +
            "Similar hooks that might match: {1}";
        private static readonly LocalizableString MessageFormatCommand = 
            "Method '{0}' is never used.\n" +
            "If you intended this to be a command, here are the common command signatures:\n" +
            "[Command(\"name\")]\n" +
            "void CommandName(IPlayer player, string command, string[] args)\n\n" +
            "[ChatCommand(\"name\")]\n" +
            "void CommandName(BasePlayer player, string command, string[] args)\n\n" +
            "[ConsoleCommand(\"name\")]\n" +
            "void CommandName(ConsoleSystem.Arg args)";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST003.md");

        // Для сравнения символов методов
        private static readonly SymbolEqualityComparer SymbolComparer = SymbolEqualityComparer.Default;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethodInvocations, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethodInvocations(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
                return;

            // Пропускаем методы, которые не нужно анализировать
            if (ShouldSkip(methodSymbol))
                return;

            // Проверяем, используется ли метод
            if (IsMethodUsed(methodSymbol, context))
                return;

            // Если метод называется "command"
            if (IsCommand(methodSymbol.Name))
            {
                ReportDiagnostic(
                    context,
                    methodSymbol,
                    MessageFormatCommand,
                    methodSymbol.Name);
                return;
            }

            
            var rustHooks = StringDistance.FindKeyValues(
                methodSymbol.Name,
                HooksConfiguration.GetSimilarHooks(methodSymbol, 3)
                    .Select(s => ("rust", s))
                    .Concat(PluginHooksConfiguration.GetSimilarHooks(methodSymbol, 3)
                        .Select(s => ($" (from plugin: {s.pluginName})", s.hookName))),
                3).Select(s => s.key == "rust" ? s.value : s.value + s.key);

            // Получаем похожие хуки Unity
            var unityHooks = UnityHooksConfiguration.GetSimilarHooks(methodSymbol, 3);
                
            // Объединяем все хуки
            var commonHooks = rustHooks.Concat(unityHooks)
                .Distinct()
                .ToList();
                
            if (commonHooks.Any())
            {
                var suggestionsText = string.Join(", ", commonHooks);

                ReportDiagnostic(
                    context,
                    methodSymbol,
                    MessageFormatWithHooks,
                    methodSymbol.Name,
                    suggestionsText);
            }
            // Иначе выводим базовый диагноз
            else
            {
                ReportDiagnostic(context, methodSymbol, MessageFormat, methodSymbol.Name);
            }
        }

        /// <summary>
        /// Решаем, нужно ли пропускать метод (например, это не обычный метод 
        /// или у него атрибуты, которые мы не анализируем, или он уже является хуком).
        /// </summary>
        private static bool ShouldSkip(IMethodSymbol methodSymbol)
        {
            if (methodSymbol.MethodKind != MethodKind.Ordinary)
                return true;

            var attributesToSkip = new[]
            {
                "ChatCommand",
                "Command",
                "ConsoleCommand",
                "HookMethod"
            };

            // Пропускаем методы с определёнными атрибутами
            if (methodSymbol.GetAttributes().Any(attr =>
            {
                var attrName = attr.AttributeClass?.Name;
                return attrName != null &&
                       (attributesToSkip.Contains(attrName) ||
                        attributesToSkip.Contains(attrName.Replace("Attribute", "")));
            }))
            {
                return true;
            }

            // Пропускаем методы, которые уже считаются хуками
            if (UnityHooksConfiguration.IsHook(methodSymbol) ||
                HooksConfiguration.IsHook(methodSymbol) ||
                PluginHooksConfiguration.IsHook(methodSymbol))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяем, используется ли метод в коде (прямые вызовы, generic-вызовы, делегаты, идентификаторы).
        /// </summary>
        private static bool IsMethodUsed(IMethodSymbol method, SyntaxNodeAnalysisContext context)
        {
            // Пропускаем override-методы, т.к. они по определению "используются"
            if (method.IsOverride)
                return true;

            var root = context.Node.SyntaxTree.GetRoot(context.CancellationToken);

            // Смотрим все вызовы методов в файле
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                var info = context.SemanticModel.GetSymbolInfo(invocation);
                if (info.Symbol is IMethodSymbol calledMethod)
                {
                    // Проверяем совпадение или совпадение через OriginalDefinition
                    if (SymbolComparer.Equals(calledMethod, method) ||
                        SymbolComparer.Equals(calledMethod.OriginalDefinition, method))
                    {
                        return true;
                    }
                }
            }

            // Проверяем использование через memberAccess, делегаты, события
            var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            foreach (var memberAccess in memberAccesses)
            {
                var info = context.SemanticModel.GetSymbolInfo(memberAccess);
                if (SymbolComparer.Equals(info.Symbol, method))
                    return true;
            }

            // Проверяем использование как идентификатор
            var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
            foreach (var identifier in identifiers)
            {
                var info = context.SemanticModel.GetSymbolInfo(identifier);
                if (SymbolComparer.Equals(info.Symbol, method))
                    return true;
            }

            return false;
        }

        private static bool IsCommand(string methodName)
        {
            // Преобразуем строку в нижний регистр для игнорирования регистра
            string methodNameLower = methodName.ToLower();

            // Проверка на наличие подстрок "command" или "cmd"
            return methodNameLower.Contains("command") || methodNameLower.Contains("cmd");
        }

        /// <summary>
        /// Локальный помощник для репорта диагностики с нужным форматным сообщением.
        /// </summary>
        private static void ReportDiagnostic(
            SyntaxNodeAnalysisContext context,
            IMethodSymbol methodSymbol,
            LocalizableString messageFormat,
            params object[] messageArgs)
        {
            var descriptor = new DiagnosticDescriptor(
                DiagnosticId,
                Title,
                messageFormat,
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: Description,
                helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST003.md");

            var diagnostic = Diagnostic.Create(descriptor, methodSymbol.Locations[0], messageArgs);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
