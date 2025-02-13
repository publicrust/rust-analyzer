using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Models;
using RustAnalyzer.src.Configuration;

namespace RustAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DeprecatedHookAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST0004";

        private static readonly string Title = "Deprecated Hook Found";
        private static readonly string MessageFormat =
            "Hook \"{0}\" is deprecated. Use \"{1}\" instead.";
        private static readonly string Description =
            "This hook has been marked as deprecated and should be replaced with the new version.";

        private const string Category = "Hook Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: $"https://github.com/rust-analyzer/docs/{DiagnosticId}.md"
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeMethodDeclaration,
                SyntaxKind.MethodDeclaration
            );
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
                return;

            DeprecatedHookModel deprecatedHook;
            if (DeprecatedHooksConfiguration.IsHook(methodSymbol, out deprecatedHook))
            {
                var parameters = methodSymbol.Parameters;
                var parameterTypes = string.Join(", ", parameters.Select(p => p.Type.ToString()));
                var methodSignature = $"{methodSymbol.Name}({parameterTypes})";

                var newHookSignature =
                    deprecatedHook?.NewHook != null
                        ? $"{deprecatedHook.NewHook.HookName}({string.Join(", ", deprecatedHook.NewHook.HookParameters)})"
                        : "no replacement";

                var diagnostic = Diagnostic.Create(
                    Rule,
                    methodDeclaration.Identifier.GetLocation(),
                    methodSignature,
                    newHookSignature
                );

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
