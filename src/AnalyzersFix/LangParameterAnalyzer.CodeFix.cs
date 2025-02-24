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
using Microsoft.CodeAnalysis.Editing;
using OxideAnalyzers;

namespace RustAnalyzer.AnalyzersFix
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LangParameterAnalyzerCodeFix)), Shared]
    public class LangParameterAnalyzerCodeFix : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => 
            ImmutableArray.Create(LangParameterAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => 
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Находим метод, содержащий избыточную логику
            var methodDeclaration = root.FindToken(diagnosticSpan.Start)
                .Parent?
                .AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (methodDeclaration == null)
                return;

            // Предлагаем исправление
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Simplify localization logic",
                    createChangedDocument: c => SimplifyLocalizationLogicAsync(context.Document, methodDeclaration, c),
                    equivalenceKey: "SimplifyLocalizationLogic"),
                diagnostic);
        }

        private async Task<Document> SimplifyLocalizationLogicAsync(
            Document document, 
            MethodDeclarationSyntax methodDecl, 
            CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var editor = new SyntaxEditor(root, document.Project.Solution.Workspace);

            // Получаем параметры метода
            var parameters = methodDecl.ParameterList.Parameters;
            var hasUserIdParam = parameters.Any(p => 
                p.Identifier.Text == "userId" || 
                p.Identifier.Text.EndsWith("Id") || 
                p.Identifier.Text.EndsWith("ID"));

            var keyParamName = "key";
            var userIdParamName = "userId";

            // Находим имя параметра ключа
            foreach (var param in parameters)
            {
                if (param.Identifier.Text == "key" || 
                    param.Identifier.Text.Contains("key") || 
                    param.Identifier.Text.Contains("Key"))
                {
                    keyParamName = param.Identifier.Text;
                }
                
                if (param.Identifier.Text == "userId" || 
                    param.Identifier.Text.EndsWith("Id") || 
                    param.Identifier.Text.EndsWith("ID"))
                {
                    userIdParamName = param.Identifier.Text;
                }
            }

            // Определяем, есть ли параметр для плагина (обычно this)
            // В большинстве случаев используется как раз this
            var pluginParam = SyntaxFactory.IdentifierName("this");

            // Создаем простую реализацию метода
            var returnStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("lang"),
                        SyntaxFactory.IdentifierName("GetMessage")),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(new[] {
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(keyParamName)),
                            SyntaxFactory.Argument(pluginParam),
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(userIdParamName))
                        }))));

            // Создаем новое тело метода
            var newBody = SyntaxFactory.Block(returnStatement);

            // Заменяем тело метода
            editor.ReplaceNode(methodDecl.Body, newBody);

            // Возвращаем обновленный документ
            return document.WithSyntaxRoot(editor.GetChangedRoot());
        }
    }
} 