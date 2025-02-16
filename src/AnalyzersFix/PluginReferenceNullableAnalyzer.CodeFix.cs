using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using RustAnalyzer.Analyzers;

namespace RustAnalyzer.AnalyzersFix
{
    [
        ExportCodeFixProvider(
            LanguageNames.CSharp,
            Name = nameof(PluginReferenceNullableCodeFixProvider)
        ),
        Shared
    ]
    public class PluginReferenceNullableCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(PluginReferenceNullableAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context
                .Document.GetSyntaxRootAsync(context.CancellationToken)
                .ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Находим объявление поля, которое нужно исправить
            var fieldDeclaration = root.FindToken(diagnosticSpan.Start)
                .Parent.AncestorsAndSelf()
                .OfType<FieldDeclarationSyntax>()
                .First();

            // Регистрируем CodeFix
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make field nullable",
                    createChangedDocument: c =>
                        MakeFieldNullableAsync(context.Document, fieldDeclaration, c),
                    equivalenceKey: nameof(PluginReferenceNullableCodeFixProvider)
                ),
                diagnostic
            );
        }

        private async Task<Document> MakeFieldNullableAsync(
            Document document,
            FieldDeclarationSyntax fieldDecl,
            CancellationToken cancellationToken
        )
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var originalType = fieldDecl.Declaration.Type;

            // Создаем новый тип с суффиксом ?
            var newType = SyntaxFactory
                .NullableType(originalType)
                .WithLeadingTrivia(originalType.GetLeadingTrivia())
                .WithTrailingTrivia(originalType.GetTrailingTrivia());

            // Создаем новое объявление поля с nullable типом
            var newFieldDecl = fieldDecl.WithDeclaration(fieldDecl.Declaration.WithType(newType));

            // Заменяем старое объявление на новое
            var newRoot = root.ReplaceNode(fieldDecl, newFieldDecl);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
