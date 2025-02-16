using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Utils;

namespace RustAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EmptyBlockAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = "Empty code block detected";

        private static readonly string NoteTemplate = "Empty code block found in {0}";

        private static readonly string HelpTemplate =
            "Code blocks should contain implementation. Empty blocks might indicate incomplete code.";

        private static readonly string ExampleTemplate =
            "// Add implementation or remove the empty block\n"
            + "void Example()\n"
            + "{\n"
            + "    // Your code here\n"
            + "    DoSomething();\n"
            + "}";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: "RUST000050",
            Title,
            "{0}", // Placeholder for dynamic description
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Code blocks should contain implementation. Empty blocks might indicate incomplete code.",
            helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST000050.md"
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
        }

        private void AnalyzeBlock(SyntaxNodeAnalysisContext context)
        {
            var block = (BlockSyntax)context.Node;

            if (IsInterfaceOrAbstractMethod(block))
                return;

            if (IsAutoPropertyAccessor(block))
                return;

            if (!block.Statements.Any())
            {
                var location = block.GetLocation();
                var sourceText = location.SourceTree?.GetText();
                if (sourceText == null)
                    return;

                var parentContext = GetParentContext(block);

                var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
                {
                    ErrorCode = "RUST000050",
                    ErrorTitle = "Empty code block detected",
                    Location = location,
                    SourceText = sourceText,
                    MessageParameters = new[] { parentContext },
                    Note = NoteTemplate,
                    Help = HelpTemplate,
                    Example = ExampleTemplate,
                };

                var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
                var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsInterfaceOrAbstractMethod(BlockSyntax block)
        {
            var parent = block.Parent;
            while (parent != null)
            {
                if (parent is InterfaceDeclarationSyntax)
                    return true;

                if (parent is MethodDeclarationSyntax method)
                    return method.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword));

                parent = parent.Parent;
            }
            return false;
        }

        private bool IsAutoPropertyAccessor(BlockSyntax block)
        {
            return block.Parent is AccessorDeclarationSyntax accessor
                && accessor.Parent is AccessorListSyntax accessorList
                && accessorList.Parent is PropertyDeclarationSyntax;
        }

        private string GetParentContext(BlockSyntax block)
        {
            var parent = block.Parent;
            while (parent != null)
            {
                switch (parent)
                {
                    case MethodDeclarationSyntax method:
                        return $"method '{method.Identifier.Text}'";
                    case PropertyDeclarationSyntax property:
                        return $"property '{property.Identifier.Text}'";
                    case AccessorDeclarationSyntax accessor:
                        return $"property accessor '{accessor.Keyword.Text}'";
                    case ConstructorDeclarationSyntax constructor:
                        return "constructor";
                    case DestructorDeclarationSyntax:
                        return "destructor";
                    case EventDeclarationSyntax @event:
                        return $"event '{@event.Identifier.Text}'";
                }
                parent = parent.Parent;
            }
            return "unknown context";
        }
    }
}
