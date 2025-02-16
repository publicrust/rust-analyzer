using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Utils;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class LoadDefaultMessagesAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST004";
        private const string Category = "Usage";

        private static readonly LocalizableString Title =
            "Incorrect LoadDefaultMessages declaration";

        private static readonly string NoteTemplate =
            "LoadDefaultMessages should be declared as protected override void";

        private static readonly string HelpTemplate =
            "Change the method declaration to match the required signature";

        private static readonly string ExampleTemplate =
            "protected override void LoadDefaultMessages()\n"
            + "{\n"
            + "    lang.RegisterMessages(new Dictionary<string, string>\n"
            + "    {\n"
            + "        [\"Example\"] = \"This is an example message!\",\n"
            + "        [\"AnotherExample\"] = \"Here is another example\"\n"
            + "    }, this);\n"
            + "}";

        private static readonly LocalizableString Description =
            "LoadDefaultMessages should be declared as protected override void.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            "{0}", // Placeholder for dynamic description
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST004.md"
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

        private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Проверяем, что это метод LoadDefaultMessages
            if (methodDeclaration.Identifier.Text != "LoadDefaultMessages")
                return;

            // Проверяем модификаторы
            bool isProtected = methodDeclaration.Modifiers.Any(SyntaxKind.ProtectedKeyword);
            bool isOverride = methodDeclaration.Modifiers.Any(SyntaxKind.OverrideKeyword);
            bool isVoid =
                methodDeclaration.ReturnType is PredefinedTypeSyntax predefinedType
                && predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword);

            if (!isProtected || !isOverride || !isVoid)
            {
                var location = methodDeclaration.Identifier.GetLocation();
                var sourceText = location.SourceTree?.GetText();
                if (sourceText == null)
                    return;

                var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
                {
                    ErrorCode = DiagnosticId,
                    ErrorTitle =
                        "Method 'LoadDefaultMessages' should be declared as 'protected override void LoadDefaultMessages()'",
                    Location = location,
                    SourceText = sourceText,
                    MessageParameters = new[] { methodDeclaration.Identifier.Text },
                    Note = NoteTemplate,
                    Help = HelpTemplate,
                    Example = ExampleTemplate,
                };

                var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
                var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
