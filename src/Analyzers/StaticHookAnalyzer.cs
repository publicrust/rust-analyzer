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
    public class StaticHookAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RA0030";
        private const string Category = "Design";

        private static readonly LocalizableString Title = "Hook method cannot be static";
        private static readonly LocalizableString MessageFormat =
            "Hook method '{0}' cannot be static. Remove the static modifier.";
        private static readonly LocalizableString Description =
            "Hook methods in Rust/Oxide plugins must be instance methods to properly handle game events.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RA0030.md"
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

            // Проверяем, является ли метод хуком
            if (!IsHook(methodSymbol))
                return;

            // Проверяем, является ли метод статическим
            if (!methodSymbol.IsStatic)
                return;

            // Находим модификатор static
            var staticModifier = methodDeclaration.Modifiers.FirstOrDefault(m =>
                m.IsKind(SyntaxKind.StaticKeyword)
            );

            if (staticModifier == default)
                return;

            // Сообщаем об ошибке
            var diagnostic = Diagnostic.Create(
                Rule,
                staticModifier.GetLocation(),
                methodSymbol.Name
            );

            context.ReportDiagnostic(diagnostic);
        }

        private bool IsHook(IMethodSymbol method)
        {
            return HooksConfiguration.IsKnownHook(method)
                || PluginHooksConfiguration.IsKnownHook(method)
                || UnityHooksConfiguration.IsKnownHook(method);
        }
    }
}
