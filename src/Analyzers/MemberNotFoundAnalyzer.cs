using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace RustAnalyzer
{
    /// <summary>
    /// Analyzer that detects attempts to use non-existent members on types.
    /// </summary>
    /// <remarks>
    /// See the full documentation at <see href="https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST006.md">RUST006: Member Not Found</see>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MemberNotFoundAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST006";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Member not found";
        private static readonly string MessageFormatTemplate =
            "error[E0599]: no {0} named `{1}` found for type `{2}` in the current scope\n" +
            "  --> {4}:{5}:{6}\n" +
            "   |\n" +
            "{5,2} | {7}\n" + // Source line
            "   | {8} {0} not found in `{2}`\n" + // Pointer and explanation
            "   |\n" +
            "   = note: the type `{2}` does not have a {0} named `{1}`\n" +
            "   = help: did you mean one of these?\n" +
            "{3}";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            "{0}", // Placeholder for dynamic description
            Category,
            DiagnosticSeverity.Error,
            helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST006.md",
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context == null) return;

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        }

        private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
        {
            var memberAccess = (MemberAccessExpressionSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var expressionInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
            var typeSymbol = expressionInfo.Type;
            var memberName = memberAccess.Name.Identifier.ValueText;

            if (typeSymbol == null)
                return;

            var symbol = semanticModel.GetSymbolInfo(memberAccess.Name).Symbol;
            if (symbol != null)
                return;

            // Retrieve all members of the type, excluding compiler-generated and inaccessible members
            var members = typeSymbol.GetMembers()
                .Where(m => !IsCompilerGenerated(m) && IsAccessibleMember(m))
                .ToList();

            // Find similar members
            var suggestions = FindSimilarMembers(memberName, members);

            // If no suggestions are found, add a fallback message
            if (string.IsNullOrEmpty(suggestions))
                suggestions = "           - (no similar members)";

            // Get the location and line information for the name node
            var nameLocation = memberAccess.Name.GetLocation();
            var lineSpan = nameLocation.GetLineSpan();
            var startLinePosition = lineSpan.StartLinePosition;

            var sourceText = nameLocation.SourceTree?.GetText();
            if (sourceText == null) return;

            var lineText = sourceText.Lines[startLinePosition.Line].ToString();

            // Expand tabs for consistent alignment
            lineText = TextAlignmentUtils.ExpandTabs(lineText, 4);

            // Column where the member name starts
            int charColumn = startLinePosition.Character;

            // Compute the "visual" column, accounting for tabs
            int visualColumn = TextAlignmentUtils.ComputeVisualColumn(lineText, charColumn, 4);
            string pointerLine = TextAlignmentUtils.CreatePointerLine(lineText, charColumn, memberName.Length, 4);

            // Retrieve the file name
            var fileName = System.IO.Path.GetFileName(nameLocation.SourceTree?.FilePath ?? string.Empty);

            // Create the filled message format for description
            var dynamicDescription = string.Format(
                MessageFormatTemplate,
                DetermineMemberKind(memberAccess, semanticModel), // {0}
                memberName,                                       // {1}
                typeSymbol.ToDisplayString(),                    // {2}
                suggestions,                                     // {3}
                fileName,                                        // {4}
                startLinePosition.Line + 1,                      // {5}
                charColumn + 1,                                  // {6}
                lineText,                                        // {7}
                pointerLine                                      // {8}
            );

            // Create the diagnostic
            var diagnostic = Diagnostic.Create(
                Rule,
                nameLocation,
                dynamicDescription,
                DetermineMemberKind(memberAccess, semanticModel), // {0}
                memberName,                                       // {1}
                typeSymbol.ToDisplayString(),                    // {2}
                suggestions,                                     // {3}
                fileName,                                        // {4}
                startLinePosition.Line + 1,                      // {5}
                charColumn + 1,                                  // {6}
                lineText,                                        // {7}
                pointerLine                                      // {8}
            );

            context.ReportDiagnostic(diagnostic);
        }

        private string DetermineMemberKind(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
        {
            if (memberAccess.Parent is InvocationExpressionSyntax)
                return "method";

            var binding = semanticModel.GetSymbolInfo(memberAccess);
            if (binding.CandidateSymbols.Any(s => s is IMethodSymbol))
                return "method";
            if (binding.CandidateSymbols.Any(s => s is IPropertySymbol))
                return "property";
            if (binding.CandidateSymbols.Any(s => s is IFieldSymbol))
                return "field";

            return "member";
        }

        private string FindSimilarMembers(string targetName, List<ISymbol> members)
        {
            var candidates = members.Select(m => (text: m.Name, context: FormatMemberSuggestion(m)));
            
            var similarMembers = StringSimilarity.FindSimilarWithContext(
                targetName,
                candidates,
                maxSuggestions: 5)
                .Select(r => r.Context) // Используем Context, так как там уже отформатированное предложение
                .ToList();

            return string.Join("\n", similarMembers);
        }

        private string FormatMemberSuggestion(ISymbol symbol)
        {
            // For methods, include their return type and signatures
            if (symbol is IMethodSymbol methodSymbol)
            {
                string returnType = methodSymbol.ReturnType.ToDisplayString();
                string parameters = string.Join(", ", methodSymbol.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                return $"           - `{returnType} {methodSymbol.Name}({parameters})`";
            }

            // For properties, include their type
            if (symbol is IPropertySymbol propertySymbol)
            {
                string propertyType = propertySymbol.Type.ToDisplayString();
                return $"           - `{propertyType} {propertySymbol.Name}`";
            }

            // For fields, include their type
            if (symbol is IFieldSymbol fieldSymbol)
            {
                string fieldType = fieldSymbol.Type.ToDisplayString();
                return $"           - `{fieldType} {fieldSymbol.Name}`";
            }

            // For other members, just return their name
            return $"           - `{symbol.Name}`";
        }

        private bool IsCompilerGenerated(ISymbol symbol)
        {
            // Exclude compiler-generated members (e.g., backing fields, display classes)
            return symbol.Name.StartsWith("<") && symbol.Name.EndsWith(">");
        }

        private bool IsAccessibleMember(ISymbol symbol)
        {
            // Only include accessible members (public, protected, internal, etc.)
            return symbol.DeclaredAccessibility != Accessibility.Private;
        }
    }
}
