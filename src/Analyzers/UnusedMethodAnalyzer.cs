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

        private static readonly string NoteTemplate = "Method '{0}' is never used";

        private static readonly string HelpTemplate =
            "Remove the method or mark it with appropriate attribute if it's intended to be used";

        private static readonly string ExampleTemplate = "// Example of proper method usage";

        private static readonly string HookNoteTemplate =
            "Method '{0}' is never used. If you intended this to be a hook, no matching hook was found";

        private static readonly string HookHelpTemplate = "Similar hooks that might match: {0}";

        private static readonly string CommandNoteTemplate =
            "Method '{0}' is never used. If you intended this to be a command, here are the common command signatures";

        private static readonly string CommandHelpTemplate =
            "[Command(\"name\")]\n"
            + "void CommandName(IPlayer player, string command, string[] args)\n\n"
            + "[ChatCommand(\"name\")]\n"
            + "void CommandName(BasePlayer player, string command, string[] args)\n\n"
            + "[ConsoleCommand(\"name\")]\n"
            + "void CommandName(ConsoleSystem.Arg args)";

        private static readonly string ApiNoteTemplate = "Method '{0}' is never used";

        private static readonly string ApiHelpTemplate =
            "If you want to expose this method as an API for other plugins, mark it with [HookMethod] attribute";

        private static readonly LocalizableString Description =
            "Methods should be used or removed to maintain clean code.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            "{0}", // Placeholder for dynamic description
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST003.md"
        );

        private static readonly SymbolEqualityComparer SymbolComparer =
            SymbolEqualityComparer.Default;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

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

            if (ShouldSkip(methodSymbol))
                return;

            if (IsMethodUsed(methodSymbol, context))
                return;

            if (HooksConfiguration.IsKnownHook(methodSymbol))
                return;

            if (PluginHooksConfiguration.IsKnownHook(methodSymbol))
                return;

            if (UnityHooksConfiguration.IsKnownHook(methodSymbol))
                return;

            if (DeprecatedHooksConfiguration.IsHook(methodSymbol))
                return;

            var location = methodDeclaration.Identifier.GetLocation();
            var sourceText = location.SourceTree?.GetText();
            if (sourceText == null)
                return;

            var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
            {
                ErrorCode = DiagnosticId,
                ErrorTitle = Title.ToString(),
                Location = location,
                SourceText = sourceText,
                MessageParameters = new[] { methodSymbol.Name },
                Note = NoteTemplate,
                Help = HelpTemplate,
                Example = ExampleTemplate,
            };

            if (IsInAPIRegion(methodDeclaration))
            {
                formatInfo.Note = ApiNoteTemplate;
                formatInfo.Help = ApiHelpTemplate;
                var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
                var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
                context.ReportDiagnostic(diagnostic);
                return;
            }

            if (CommandUtils.IsCommand(methodSymbol))
            {
                formatInfo.Note = CommandNoteTemplate;
                formatInfo.Help = CommandHelpTemplate;
                var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
                var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
                context.ReportDiagnostic(diagnostic);
                return;
            }

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
                var suggestionsText = string.Join("\n           ", allSimilarHooks);
                formatInfo.Note = HookNoteTemplate;
                formatInfo.Help = string.Format(HookHelpTemplate, suggestionsText);
            }

            var dynamicDesc = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
            var diag = Diagnostic.Create(Rule, location, dynamicDesc);
            context.ReportDiagnostic(diag);
        }

        private static IEnumerable<string> GetHookParameters(string hookName)
        {
            var hook = PluginHooksConfiguration.GetPluginInfo(hookName);
            if (hook == null)
                return Enumerable.Empty<string>();

            return hook.Parameters.Select(p => p.Type);
        }

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

        private static bool IsMethodUsed(IMethodSymbol method, SyntaxNodeAnalysisContext context)
        {
            if (method.IsOverride)
                return true;

            if (method.IsExtensionMethod)
                return true;

            var compilation = context.Compilation;

            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot(context.CancellationToken);

                var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    var info = semanticModel.GetSymbolInfo(invocation);
                    if (info.Symbol is IMethodSymbol calledMethod)
                    {
                        if (
                            calledMethod.Name == "AddConsoleCommand"
                            || calledMethod.Name == "AddChatCommand"
                        )
                        {
                            var args = invocation.ArgumentList.Arguments;
                            if (args.Count >= 3)
                            {
                                var methodNameArg = args[2].Expression;

                                var constValue = semanticModel.GetConstantValue(methodNameArg);
                                if (
                                    constValue.HasValue
                                    && constValue.Value is string methodName
                                    && methodName == method.Name
                                )
                                {
                                    return true;
                                }

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

                        if (
                            SymbolComparer.Equals(calledMethod, method)
                            || SymbolComparer.Equals(calledMethod.OriginalDefinition, method)
                        )
                        {
                            return true;
                        }
                    }
                }

                var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
                foreach (var memberAccess in memberAccesses)
                {
                    var info = semanticModel.GetSymbolInfo(memberAccess);
                    if (SymbolComparer.Equals(info.Symbol, method))
                        return true;
                }

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
            var syntaxTree = methodDeclaration.SyntaxTree;
            var position = methodDeclaration.SpanStart;

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

            return regionStack.Count > 0 && regionStack.Peek() == "API";
        }
    }
}
