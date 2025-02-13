using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using RustAnalyzer.Configuration;
using RustAnalyzer.src.Configuration;
using RustAnalyzer.Utils;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnusedMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST003";
        private const string Category = "Design";

        private static readonly LocalizableString Title = "Unused method detected";
        private static readonly LocalizableString Description =
            "Methods should be used or removed to maintain clean code.";

        private static readonly LocalizableString MessageFormat = "Method '{0}' is never used";
        private static readonly LocalizableString MessageFormatWithHooks =
            "Method '{0}' is never used.\n"
            + "If you intended this to be a hook, no matching hook was found.\n"
            + "Similar hooks that might match: {1}";
        private static readonly LocalizableString MessageFormatCommand =
            "Method '{0}' is never used.\n"
            + "If you intended this to be a command, here are the common command signatures:\n"
            + "[Command(\"name\")]\n"
            + "void CommandName(IPlayer player, string command, string[] args)\n\n"
            + "[ChatCommand(\"name\")]\n"
            + "void CommandName(BasePlayer player, string command, string[] args)\n\n"
            + "[ConsoleCommand(\"name\")]\n"
            + "void CommandName(ConsoleSystem.Arg args)";

        private static readonly LocalizableString MessageFormatAPI =
            "Method '{0}' is never used.\n"
            + "If you want to expose this method as an API for other plugins, mark it with [HookMethod] attribute.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST003.md"
        );

        // Для сравнения символов методов
        private static readonly SymbolEqualityComparer SymbolComparer =
            SymbolEqualityComparer.Default;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeMethodInvocations,
                SyntaxKind.MethodDeclaration
            );
        }

        private static void AnalyzeMethodInvocations(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
                return;

            if (
                !HooksUtils.IsRustClass(methodSymbol.ContainingType)
                && !HooksUtils.IsUnityClass(methodSymbol.ContainingType)
            )
                return;

            // Пропускаем методы, которые не нужно анализировать
            if (ShouldSkip(methodSymbol))
                return;

            // Проверяем, используется ли метод
            if (IsMethodUsed(methodSymbol, context))
                return;

            // Проверяем хуки
            if (HooksConfiguration.IsKnownHook(methodSymbol))
                return;

            if (PluginHooksConfiguration.IsKnownHook(methodSymbol))
                return;

            if (UnityHooksConfiguration.IsKnownHook(methodSymbol))
                return;

            if (DeprecatedHooksConfiguration.IsHook(methodSymbol))
                return;

            // Если метод в регионе API, выдаем специальное сообщение
            if (IsInAPIRegion(methodDeclaration))
            {
                ReportDiagnostic(context, methodSymbol, MessageFormatAPI, methodSymbol.Name);
                return;
            }

            // Если метод называется "command" и не используется через AddConsoleCommand
            if (CommandUtils.IsCommand(methodSymbol))
            {
                ReportDiagnostic(context, methodSymbol, MessageFormatCommand, methodSymbol.Name);
                return;
            }

            // Получаем похожие хуки из всех источников
            var similarHooks = HooksConfiguration
                .GetSimilarHooks(methodSymbol)
                .Select(h => h.ToString());

            var similarPluginHooks = PluginHooksConfiguration
                .GetSimilarHooks(methodSymbol)
                .Select(h => h.ToString() + $" (from plugin: {h.PluginName})");

            var similarUnityHooks = UnityHooksConfiguration
                .GetSimilarHooks(methodSymbol)
                .Select(h => h.ToString());

            var allSimilarHooks = similarHooks.Concat(similarPluginHooks).Concat(similarUnityHooks);

            if (allSimilarHooks.Any())
            {
                var suggestionsText = string.Join(", ", allSimilarHooks);

                ReportDiagnostic(
                    context,
                    methodSymbol,
                    MessageFormatWithHooks,
                    methodSymbol.Name,
                    suggestionsText
                );
            }
            else
            {
                ReportDiagnostic(context, methodSymbol, MessageFormat, methodSymbol.Name);
            }
        }

        private static IEnumerable<string> GetHookParameters(string hookName)
        {
            var hook = PluginHooksConfiguration.GetPluginInfo(hookName);
            if (hook == null)
                return Enumerable.Empty<string>();

            return hook.HookParameters.Select(p => p.Type);
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
                "HookMethod",
            };

            // Пропускаем методы с определёнными атрибутами
            if (
                methodSymbol
                    .GetAttributes()
                    .Any(attr =>
                    {
                        var attrName = attr.AttributeClass?.Name;
                        return attrName != null
                            && (
                                attributesToSkip.Contains(attrName)
                                || attributesToSkip.Contains(attrName.Replace("Attribute", ""))
                            );
                    })
            )
            {
                return true;
            }

            // Пропускаем методы, которые уже считаются хуками
            if (
                UnityHooksConfiguration.IsHook(methodSymbol)
                || HooksConfiguration.IsHook(methodSymbol)
                || PluginHooksConfiguration.IsHook(methodSymbol)
            )
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

            // Пропускаем методы расширения, так как они могут использоваться в других местах
            if (method.IsExtensionMethod)
                return true;

            var compilation = context.Compilation;

            // Проходим по всем SyntaxTree в компиляции
            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot(context.CancellationToken);

                // Проверяем регистрацию через все варианты команд
                var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    var info = semanticModel.GetSymbolInfo(invocation);
                    if (info.Symbol is IMethodSymbol calledMethod)
                    {
                        // Проверяем прямые вызовы AddConsoleCommand/AddChatCommand
                        if (
                            calledMethod.Name == "AddConsoleCommand"
                            || calledMethod.Name == "AddChatCommand"
                        )
                        {
                            var args = invocation.ArgumentList.Arguments;
                            if (args.Count >= 3)
                            {
                                // Проверяем третий аргумент (может быть nameof или делегат)
                                var methodNameArg = args[2].Expression;

                                // Проверяем случай с nameof
                                var constValue = semanticModel.GetConstantValue(methodNameArg);
                                if (
                                    constValue.HasValue
                                    && constValue.Value is string methodName
                                    && methodName == method.Name
                                )
                                {
                                    return true;
                                }

                                // Проверяем случай с делегатом
                                if (methodNameArg is SimpleLambdaExpressionSyntax lambda)
                                {
                                    var lambdaSymbol = semanticModel.GetSymbolInfo(lambda).Symbol;
                                    if (
                                        lambdaSymbol != null
                                        && lambdaSymbol.ContainingSymbol.Equals(
                                            method,
                                            SymbolEqualityComparer.Default
                                        )
                                    )
                                    {
                                        return true;
                                    }
                                }

                                // Проверяем случай с прямой передачей метода
                                var argSymbol = semanticModel.GetSymbolInfo(methodNameArg).Symbol;
                                if (
                                    argSymbol != null
                                    && argSymbol.Equals(method, SymbolEqualityComparer.Default)
                                )
                                {
                                    return true;
                                }
                            }
                        }

                        // Проверяем прямые вызовы метода
                        if (
                            SymbolComparer.Equals(calledMethod, method)
                            || SymbolComparer.Equals(calledMethod.OriginalDefinition, method)
                        )
                        {
                            return true;
                        }
                    }
                }

                // Проверяем использование через memberAccess, делегаты, события
                var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
                foreach (var memberAccess in memberAccesses)
                {
                    var info = semanticModel.GetSymbolInfo(memberAccess);
                    if (SymbolComparer.Equals(info.Symbol, method))
                        return true;
                }

                // Проверяем использование как идентификатор
                var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
                foreach (var identifier in identifiers)
                {
                    var info = semanticModel.GetSymbolInfo(identifier);
                    if (SymbolComparer.Equals(info.Symbol, method))
                        return true;
                }
            }

            return false;
        }

        private static bool IsInAPIRegion(MethodDeclarationSyntax methodDeclaration)
        {
            // Получаем все триви до конца файла, чтобы найти все регионы
            var syntaxTree = methodDeclaration.SyntaxTree;
            var position = methodDeclaration.SpanStart;

            // Получаем все триви в файле
            var root = syntaxTree.GetRoot();
            var triviaList = root.DescendantTrivia()
                .Where(t =>
                    t.IsKind(SyntaxKind.RegionDirectiveTrivia)
                    || t.IsKind(SyntaxKind.EndRegionDirectiveTrivia)
                )
                .OrderBy(t => t.SpanStart);

            var regionStack = new Stack<string>();

            foreach (var trivia in triviaList)
            {
                if (trivia.SpanStart > position)
                    break;

                if (trivia.IsKind(SyntaxKind.RegionDirectiveTrivia))
                {
                    var regionDirective = (RegionDirectiveTriviaSyntax)trivia.GetStructure();
                    // Получаем текст после #region
                    var regionText = regionDirective
                        .EndOfDirectiveToken.LeadingTrivia.ToString()
                        .Trim()
                        .ToUpperInvariant();

                    regionStack.Push(regionText);
                }
                else if (trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
                {
                    if (regionStack.Count > 0)
                        regionStack.Pop();
                }
            }

            // Проверяем, находимся ли мы в API регионе
            return regionStack.Count > 0 && regionStack.Peek() == "API";
        }

        /// <summary>
        /// Локальный помощник для репорта диагностики с нужным форматным сообщением.
        /// </summary>
        private static void ReportDiagnostic(
            SyntaxNodeAnalysisContext context,
            IMethodSymbol methodSymbol,
            LocalizableString messageFormat,
            params object[] messageArgs
        )
        {
            var descriptor = new DiagnosticDescriptor(
                DiagnosticId,
                Title,
                messageFormat,
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: Description,
                helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST003.md"
            );

            var diagnostic = Diagnostic.Create(descriptor, methodSymbol.Locations[0], messageArgs);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
