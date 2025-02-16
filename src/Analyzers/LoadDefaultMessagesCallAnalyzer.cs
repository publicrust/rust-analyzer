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
    public sealed class LoadDefaultMessagesCallAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST006";
        private const string Category = "Usage";

        private static readonly LocalizableString Title =
            "Direct call to LoadDefaultMessages detected";

        private static readonly string NoteTemplate =
            "Method 'LoadDefaultMessages' is automatically called by the plugin system";

        private static readonly string HelpTemplate =
            "Remove the direct call and declare the method as:";

        private static readonly string ExampleTemplate =
            "protected override void LoadDefaultMessages()\n"
            + "{\n"
            + "    lang.RegisterMessages(new Dictionary<string, string>\n"
            + "    {\n"
            + "        [\"Example\"] = \"This is an example message!\"\n"
            + "    }, this);\n"
            + "}";

        private static readonly LocalizableString Description =
            "LoadDefaultMessages is automatically called by the plugin system and should not be called directly.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            "{0}", // Placeholder for dynamic description
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST006.md"
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Проверяем, что это вызов метода
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                // Проверяем, что вызывается LoadDefaultMessages
                if (memberAccess.Name.Identifier.Text == "LoadDefaultMessages")
                {
                    ReportDiagnostic(context, memberAccess.Name.GetLocation());
                }
            }
            // Проверяем прямой вызов метода (без this.)
            else if (
                invocation.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.Text == "LoadDefaultMessages"
            )
            {
                ReportDiagnostic(context, identifier.GetLocation());
            }
        }

        private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, Location location)
        {
            var sourceText = location.SourceTree?.GetText();
            if (sourceText == null)
                return;

            var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
            {
                ErrorCode = DiagnosticId,
                ErrorTitle = "Direct call to LoadDefaultMessages is not allowed",
                Location = location,
                SourceText = sourceText,
                MessageParameters = new string[0],
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
