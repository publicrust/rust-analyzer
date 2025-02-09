using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RegisterMessagesLocationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST005";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Incorrect RegisterMessages location";
        private static readonly LocalizableString MessageFormat = 
            "Method 'RegisterMessages' should only be called inside 'protected override void LoadDefaultMessages()'\n" +
            "Example:\n" +
            "protected override void LoadDefaultMessages()\n" +
            "{\n" +
            "    lang.RegisterMessages(new Dictionary<string, string>\n" +
            "    {\n" +
            "        [\"Example\"] = \"This is an example message!\",\n" +
            "        [\"AnotherExample\"] = \"Here is another example\"\n" +
            "    }, this);\n" +
            "}";
        private static readonly LocalizableString Description = "RegisterMessages should only be called inside LoadDefaultMessages method.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST005.md");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            
            // Проверяем, что это вызов RegisterMessages
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "RegisterMessages")
            {
                // Ищем содержащий метод
                var containingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                
                // Проверяем, что метод существует и это LoadDefaultMessages с правильными модификаторами
                if (containingMethod == null || 
                    containingMethod.Identifier.Text != "LoadDefaultMessages" ||
                    !containingMethod.Modifiers.Any(SyntaxKind.ProtectedKeyword) ||
                    !containingMethod.Modifiers.Any(SyntaxKind.OverrideKeyword))
                {
                    var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
} 