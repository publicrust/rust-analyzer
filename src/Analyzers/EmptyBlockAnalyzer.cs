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
    public class EmptyBlockAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: "RUST000050",
            title: "Empty code block detected",
            messageFormat: "Empty code block found in {0}",
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
                var parentContext = GetParentContext(block);
                var diagnostic = Diagnostic.Create(Rule, block.GetLocation(), parentContext);
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
                if (parent is MethodDeclarationSyntax method)
                    return $"method '{method.Identifier.Text}'";
                if (parent is ConstructorDeclarationSyntax ctor)
                    return $"constructor '{ctor.Identifier.Text}'";
                if (parent is PropertyDeclarationSyntax prop)
                    return $"property '{prop.Identifier.Text}'";
                if (parent is IfStatementSyntax)
                    return "if statement";
                if (parent is ForStatementSyntax)
                    return "for loop";
                if (parent is ForEachStatementSyntax)
                    return "foreach loop";
                if (parent is WhileStatementSyntax)
                    return "while loop";
                if (parent is DoStatementSyntax)
                    return "do-while loop";
                if (parent is SwitchSectionSyntax)
                    return "switch case";
                if (parent is TryStatementSyntax)
                    return "try block";
                if (parent is CatchClauseSyntax)
                    return "catch block";
                if (parent is FinallyClauseSyntax)
                    return "finally block";

                parent = parent.Parent;
            }
            return "code block";
        }
    }
}
