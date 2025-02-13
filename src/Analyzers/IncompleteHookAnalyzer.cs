using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RustAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IncompleteHookAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST002";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Hook method has incorrect parameters";

        private static readonly LocalizableString MessageFormat =
            "Hook \"{0}\" has missing or incorrect parameters. Expected: {1}";

        private static readonly LocalizableString Description =
            "Hook methods must be implemented with the correct parameter types to be called by Oxide";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
            {
                return;
            }

            // Check if it's a known hook
            if (HooksConfiguration.IsKnownHook(methodSymbol))
            {
                if (!HooksConfiguration.IsHook(methodSymbol))
                {
                    // Get the expected hook signature
                    var expectedSignature = HooksConfiguration
                        .HookSignatures.Where(h => h.HookName == methodSymbol.Name)
                        .Select(s => s.ToString())
                        .ToArray();

                    if (expectedSignature != null)
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule,
                            methodDeclaration.Identifier.GetLocation(),
                            methodSymbol.Name,
                            string.Join(",", expectedSignature)
                        );

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
