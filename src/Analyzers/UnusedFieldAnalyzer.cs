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
    public class UnusedFieldAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: "RUST000030",
            title: "Unused field detected",
            messageFormat: "Field '{0}' is declared but never used",
            category: "Performance",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Fields that are never used should be removed to improve code clarity.",
            helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST000030.md"
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private void AnalyzeField(SymbolAnalysisContext context)
        {
            var fieldSymbol = (IFieldSymbol)context.Symbol;

            // Пропускаем поля, помеченные атрибутами
            if (fieldSymbol.GetAttributes().Any())
                return;

            // Пропускаем поля в интерфейсах и абстрактных классах
            if (
                fieldSymbol.ContainingType.IsAbstract
                || fieldSymbol.ContainingType.TypeKind == TypeKind.Interface
            )
                return;

            // Получаем все места использования поля
            var syntaxReferences = fieldSymbol.DeclaringSyntaxReferences;
            var declarationReference = syntaxReferences.First();

            // Проверяем, есть ли использования поля в коде
            var isUsed = false;
            var root = declarationReference.SyntaxTree.GetRoot();
            var fieldNode = root.FindNode(declarationReference.Span);

            // Ищем все идентификаторы с таким же именем во всех файлах решения
            foreach (var tree in context.Compilation.SyntaxTrees)
            {
                var semanticModel = context.Compilation.GetSemanticModel(tree);
                var identifiers = tree.GetRoot()
                    .DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(id => id.Identifier.ValueText == fieldSymbol.Name);

                foreach (var identifier in identifiers)
                {
                    // Пропускаем само объявление
                    if (
                        tree == declarationReference.SyntaxTree
                        && (
                            identifier.Parent == fieldNode || identifier.Parent?.Parent == fieldNode
                        )
                    )
                        continue;

                    // Проверяем, что это действительно ссылка на наше поле
                    var symbolInfo = semanticModel.GetSymbolInfo(identifier);

                    if (
                        symbolInfo.Symbol != null
                        && SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, fieldSymbol)
                    )
                    {
                        isUsed = true;
                        break;
                    }
                }

                if (isUsed)
                    break;
            }

            if (!isUsed)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    fieldSymbol.Locations[0],
                    fieldSymbol.Name
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
