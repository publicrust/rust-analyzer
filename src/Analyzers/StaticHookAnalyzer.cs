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

        private static readonly string NoteTemplate = "Hook method '{0}' cannot be static";

        private static readonly string HelpTemplate = "Remove the static modifier from the method";

        private static readonly LocalizableString Description =
            "Hook methods in Rust/Oxide plugins must be instance methods to properly handle game events.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            "{0}", // Placeholder for dynamic description
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

            // Проверяем, является ли метод статическим и находим модификатор static
            var staticModifier = methodDeclaration.Modifiers.FirstOrDefault(m =>
                m.IsKind(SyntaxKind.StaticKeyword)
            );

            if (staticModifier == default)
                return;

            // Проверяем, является ли метод хуком
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
            if (methodSymbol == null || !IsHookMethod(methodSymbol))
                return;

            var location = staticModifier.GetLocation();
            var sourceText = location.SourceTree?.GetText();
            if (sourceText == null)
                return;

            // Создаем пример исправления на основе текущего метода
            var exampleTemplate = CreateExampleFromMethod(methodDeclaration);

            var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
            {
                ErrorCode = DiagnosticId,
                ErrorTitle = "Static hook methods are not allowed",
                Location = location,
                SourceText = sourceText,
                MessageParameters = new[] { methodDeclaration.Identifier.Text },
                Note = NoteTemplate,
                Help = HelpTemplate,
                Example = exampleTemplate,
            };

            var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
            var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
            context.ReportDiagnostic(diagnostic);
        }

        private string CreateExampleFromMethod(MethodDeclarationSyntax method)
        {
            // Получаем все модификаторы кроме static
            var modifiers = method
                .Modifiers.Where(m => !m.IsKind(SyntaxKind.StaticKeyword))
                .Select(m => m.Text);

            // Если нет public/private/protected, добавляем public
            if (!modifiers.Any(m => m is "public" or "private" or "protected"))
                modifiers = modifiers.Prepend("public");

            var modifiersText = string.Join(" ", modifiers);
            var returnType = method.ReturnType.ToString();
            var parameters = method.ParameterList.ToString();

            return $"{modifiersText} {returnType} {method.Identifier.Text}{parameters}";
        }

        private bool IsHookMethod(IMethodSymbol methodSymbol)
        {
            return HooksConfiguration.IsKnownHook(methodSymbol);
        }
    }
}
