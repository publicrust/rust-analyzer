using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Utils;

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

        private static readonly string NoteTemplate =
            "the type `{2}` does not have a {0} named `{1}`";
        private static readonly string HelpTemplate = "did you mean one of these?";

        private static readonly LocalizableString Description = "Member not found in type";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            "{0}", // Placeholder for dynamic description
            Category,
            DiagnosticSeverity.Error,
            helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST006.md",
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                return;

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(
                AnalyzeMemberAccess,
                SyntaxKind.SimpleMemberAccessExpression
            );
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

            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Name);
            var symbol = symbolInfo.Symbol;
            
            // Проверяем наличие символа и его доступность
            if (symbol != null)
            {
                // Если символ найден и это статический член, но доступ идет через экземпляр - сообщаем об ошибке
                if (symbol.IsStatic && !memberAccess.Expression.IsKind(SyntaxKind.IdentifierName))
                {
                    // Это статический член, к которому обращаются через экземпляр - пропускаем,
                    // так как это другая ошибка (CS0176)
                    return;
                }
                
                // Если символ найден и всё в порядке - выходим
                return;
            }

            // Проверяем кандидатов, если основной символ не найден
            if (symbolInfo.CandidateSymbols.Length > 0)
            {
                // Если есть подходящие кандидаты - значит это другая проблема (например, неоднозначность)
                return;
            }

            // Получаем все члены типа, включая статические
            var members = typeSymbol
                .GetMembers()
                .Where(m => !IsCompilerGenerated(m) && IsAccessibleMember(m))
                .ToList();

            // Добавляем статические члены из статического типа
            var staticType = semanticModel.GetTypeInfo(memberAccess.Expression).Type as INamedTypeSymbol;
            if (staticType != null)
            {
                members.AddRange(
                    staticType.GetMembers()
                        .Where(m => m.IsStatic && !IsCompilerGenerated(m) && IsAccessibleMember(m))
                );
            }

            // Find similar members
            var suggestions = FindSimilarMembers(memberName, members);

            var memberKind = DetermineMemberKind(memberAccess, semanticModel);
            var location = memberAccess.Name.GetLocation();
            var sourceText = location.SourceTree?.GetText();
            if (sourceText == null)
                return;

            var parameters = new[]
            {
                memberKind,
                memberName,
                typeSymbol.ToDisplayString(),
            };

            var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
            {
                ErrorCode = "E0599",
                ErrorTitle =
                    $"no {memberKind} named `{memberName}` found for type `{typeSymbol.ToDisplayString()}` in the current scope",
                Location = location,
                SourceText = sourceText,
                MessageParameters = parameters,
                Note = NoteTemplate,
                Help = HelpTemplate,
                Example = suggestions,
            };

            var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
            var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
            context.ReportDiagnostic(diagnostic);
        }

        private string DetermineMemberKind(
            MemberAccessExpressionSyntax memberAccess,
            SemanticModel semanticModel
        )
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
            var candidates = members.Select(m =>
                (text: m.Name, context: FormatMemberSuggestion(m))
            );

            var similarMembers = StringSimilarity
                .FindSimilarWithContext(targetName, candidates, maxSuggestions: 5)
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
                string parameters = string.Join(
                    ", ",
                    methodSymbol.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}")
                );
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
