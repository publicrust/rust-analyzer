using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RustAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NamespaceAnalyzer : DiagnosticAnalyzer
    {
        private const string RequiredNamespace = "Oxide.Plugins";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: "RUST000020",
            title: "Invalid plugin namespace",
            messageFormat: "Rust plugins must be in the '{0}' namespace. Current namespace: '{1}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "All Rust plugins must be defined in the Oxide.Plugins namespace.",
            helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST000020.md"
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeNamespace, SyntaxKind.NamespaceDeclaration);
        }

        private void AnalyzeNamespace(SyntaxNodeAnalysisContext context)
        {
            var namespaceDeclaration = (NamespaceDeclarationSyntax)context.Node;
            var namespaceName = namespaceDeclaration.Name.ToString();

            // Проверяем, есть ли в пространстве имён класс, унаследованный от RustPlugin
            var hasRustPlugin = namespaceDeclaration
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Any(classDecl =>
                    classDecl.BaseList?.Types.Any(baseType =>
                        baseType.ToString().Contains("RustPlugin")
                    ) == true
                );

            if (!hasRustPlugin)
                return;

            // Если это плагин, но пространство имён не Oxide.Plugins
            if (namespaceName != RequiredNamespace)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    namespaceDeclaration.Name.GetLocation(),
                    RequiredNamespace,
                    namespaceName
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
