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

namespace RustAnalyzer.Analyzers
{
    [
        ExportCodeFixProvider(
            LanguageNames.CSharp,
            Name = nameof(StaticHookAnalyzerCodeFixProvider)
        ),
        Shared
    ]
    public class StaticHookAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(StaticHookAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context
                .Document.GetSyntaxRootAsync(context.CancellationToken)
                .ConfigureAwait(false);

            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var declaration = root.FindToken(diagnosticSpan.Start)
                .Parent?.AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .First();

            if (declaration == null)
                return;

            // Регистрируем CodeAction
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove static modifier",
                    createChangedDocument: c =>
                        RemoveStaticModifierAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(StaticHookAnalyzerCodeFixProvider)
                ),
                diagnostic
            );
        }

        private async Task<Document> RemoveStaticModifierAsync(
            Document document,
            MethodDeclarationSyntax methodDecl,
            CancellationToken cancellationToken
        )
        {
            // Создаем новую декларацию метода без модификатора static
            var newMethodDecl = methodDecl
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        methodDecl.Modifiers.Where(m => !m.IsKind(SyntaxKind.StaticKeyword))
                    )
                )
                .WithAdditionalAnnotations(Formatter.Annotation);

            // Получаем корень документа
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            if (root == null)
                return document;

            // Заменяем старую декларацию на новую
            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);

            // Возвращаем обновленный документ
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
