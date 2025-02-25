using System.Collections.Generic;
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
            Name = nameof(PluginReferenceDependencyCodeFixProvider)
        ),
        Shared
    ]
    public class PluginReferenceDependencyCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(PluginReferenceDependencyAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context
                .Document.GetSyntaxRootAsync(context.CancellationToken)
                .ConfigureAwait(false);

            if (root == null)
                return;

            // Получаем все поля, которые нужно исправить
            var fieldNames = new HashSet<string>();
            var classDeclaration = default(ClassDeclarationSyntax);

            foreach (var diagnostic in context.Diagnostics)
            {
                if (diagnostic.Properties.TryGetValue("FieldNames", out var fields))
                {
                    foreach (var field in fields.Split(','))
                    {
                        fieldNames.Add(field);
                    }
                }

                var diagnosticSpan = diagnostic.Location.SourceSpan;
                var fieldDeclaration = root.FindToken(diagnosticSpan.Start)
                    .Parent?.AncestorsAndSelf()
                    .OfType<FieldDeclarationSyntax>()
                    .First();

                if (fieldDeclaration == null)
                    continue;

                var currentClass = fieldDeclaration.Parent as ClassDeclarationSyntax;
                if (currentClass == null)
                    continue;

                if (classDeclaration == null)
                {
                    classDeclaration = currentClass;
                }
            }

            if (classDeclaration == null || !fieldNames.Any())
                return;

            // Регистрируем один CodeFix для всех полей
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: fieldNames.Count == 1
                        ? $"Add dependency check for {fieldNames.First()}"
                        : $"Add dependency checks for {string.Join(", ", fieldNames)}",
                    createChangedDocument: c =>
                        AddDependencyChecksAsync(
                            context.Document,
                            fieldNames.ToList(),
                            classDeclaration,
                            c
                        ),
                    equivalenceKey: nameof(PluginReferenceDependencyCodeFixProvider)
                ),
                context.Diagnostics
            );
        }

        private MethodDeclarationSyntax FindOrCreateOnServerInitialized(
            ClassDeclarationSyntax classDecl
        )
        {
            // Ищем существующий метод
            var existingMethod = classDecl
                .Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "OnServerInitialized");

            if (existingMethod != null)
                return existingMethod;

            // Создаем новый метод
            return SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "OnServerInitialized"
                )
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                )
                .WithParameterList(
                    SyntaxFactory
                        .ParameterList()
                        .AddParameters(
                            SyntaxFactory
                                .Parameter(SyntaxFactory.Identifier("initial"))
                                .WithType(
                                    SyntaxFactory.PredefinedType(
                                        SyntaxFactory.Token(SyntaxKind.BoolKeyword)
                                    )
                                )
                        )
                )
                .WithBody(SyntaxFactory.Block());
        }

        private async Task<Document> AddDependencyChecksAsync(
            Document document,
            List<string> fieldNames,
            ClassDeclarationSyntax classDecl,
            CancellationToken cancellationToken
        )
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null)
                return document;

            // Находим или создаем метод OnServerInitialized
            var onServerInitialized = FindOrCreateOnServerInitialized(classDecl);
            var newOnServerInitialized = onServerInitialized;

            if (onServerInitialized.Body == null)
            {
                newOnServerInitialized = onServerInitialized.WithBody(SyntaxFactory.Block());
            }

            // Создаем все проверки на null
            var existingChecks = newOnServerInitialized
                .Body!.Statements.OfType<IfStatementSyntax>()
                .Select(ifStmt =>
                {
                    if (ifStmt.Condition is BinaryExpressionSyntax binExpr)
                    {
                        if (binExpr.Left is IdentifierNameSyntax idName)
                        {
                            return idName.Identifier.Text;
                        }
                    }
                    return string.Empty;
                })
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            var newChecks = fieldNames
                .Where(name => !existingChecks.Contains(name))
                .Select(name =>
                {
                    return CreateNullCheck(name);
                })
                .ToList();

            if (newChecks.Any())
            {
                // Добавляем все проверки в начало метода
                var newBody = newOnServerInitialized.Body!.WithStatements(
                    SyntaxFactory.List(newChecks.Concat(newOnServerInitialized.Body.Statements))
                );

                newOnServerInitialized = newOnServerInitialized
                    .WithBody(newBody)
                    .WithAdditionalAnnotations(Formatter.Annotation);

                // Если метод не существует, добавляем его в класс
                if (!classDecl.Members.Contains(onServerInitialized))
                {
                    var newClassDecl = classDecl.AddMembers(newOnServerInitialized);
                    root = root.ReplaceNode(classDecl, newClassDecl);
                }
                else
                {
                    root = root.ReplaceNode(onServerInitialized, newOnServerInitialized);
                }
            }

            return document.WithSyntaxRoot(root);
        }

        private StatementSyntax CreateNullCheck(string fieldName)
        {
            // if (Field == null) { PrintError("..."); NextTick(...); return; }
            return SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression(
                    SyntaxKind.EqualsExpression,
                    SyntaxFactory.IdentifierName(fieldName),
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                ),
                SyntaxFactory.Block(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.IdentifierName("PrintError"),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(
                                        SyntaxFactory.LiteralExpression(
                                            SyntaxKind.StringLiteralExpression,
                                            SyntaxFactory.Literal(fieldName + " plugin is not installed!")
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );
        }
    }
}
