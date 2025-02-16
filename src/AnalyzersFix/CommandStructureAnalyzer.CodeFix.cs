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
using RustAnalyzer.Configuration;

namespace RustAnalyzer.AnalyzersFix
{
    [
        ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CommandStructureCodeFixProvider)),
        Shared
    ]
    public class CommandStructureCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(CommandStructureAnalyzer.DiagnosticId);

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
            var methodDeclaration = root.FindToken(diagnosticSpan.Start)
                .Parent?.AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .First();

            if (methodDeclaration == null)
                return;

            // Определяем тип команды по атрибутам
            var commandType = await DetermineCommandTypeAsync(
                context.Document,
                methodDeclaration,
                context.CancellationToken
            );
            if (commandType == null)
                return;

            // Регистрируем CodeAction для исправления структуры команды
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Fix {commandType} structure",
                    createChangedDocument: c =>
                        FixCommandStructureAsync(
                            context.Document,
                            methodDeclaration,
                            commandType,
                            c
                        ),
                    equivalenceKey: nameof(CommandStructureCodeFixProvider)
                ),
                diagnostic
            );
        }

        private async Task<string> DetermineCommandTypeAsync(
            Document document,
            MethodDeclarationSyntax methodDeclaration,
            CancellationToken cancellationToken
        )
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null)
                return null;

            var methodSymbol = semanticModel.GetDeclaredSymbol(
                methodDeclaration,
                cancellationToken
            );
            if (methodSymbol == null)
                return null;

            // Проверяем атрибуты команд
            foreach (var attribute in methodSymbol.GetAttributes())
            {
                var attributeName = attribute.AttributeClass?.Name;
                if (attributeName == null)
                    continue;

                // Убираем суффикс "Attribute" если есть
                var commandType = attributeName.EndsWith("Attribute")
                    ? attributeName.Substring(0, attributeName.Length - "Attribute".Length)
                    : attributeName;

                if (CommandStructureInfo.CommandStructures.ContainsKey(commandType))
                    return commandType;
            }

            // Если атрибутов нет, пытаемся определить тип по параметрам
            foreach (var structure in CommandStructureInfo.CommandStructures)
            {
                if (IsMatchingStructure(methodDeclaration, structure.Value))
                    return structure.Key;
            }

            return null;
        }

        private bool IsMatchingStructure(MethodDeclarationSyntax method, CommandStructure structure)
        {
            var parameters = method.ParameterList.Parameters;
            if (parameters.Count != structure.ParameterTypes.Length)
                return false;

            // Проверяем только типы параметров, имена не важны
            for (int i = 0; i < parameters.Count; i++)
            {
                var parameterType = parameters[i].Type?.ToString();
                if (
                    parameterType == null
                    || !IsMatchingType(parameterType, structure.ParameterTypes[i])
                )
                    return false;
            }

            return true;
        }

        private bool IsMatchingType(string actualType, string expectedType)
        {
            // Обрабатываем специальные случаи
            if (expectedType == "ConsoleSystem.Arg" && actualType.EndsWith("ConsoleSystem.Arg"))
                return true;

            return actualType == expectedType
                || actualType == $"global::{expectedType}"
                || actualType == $"Oxide.Game.Rust.Libraries.{expectedType}"
                || actualType == $"Oxide.Core.Libraries.{expectedType}";
        }

        private async Task<Document> FixCommandStructureAsync(
            Document document,
            MethodDeclarationSyntax methodDecl,
            string commandType,
            CancellationToken cancellationToken
        )
        {
            var structure = CommandStructureInfo.CommandStructures[commandType];
            var parameters = new List<ParameterSyntax>();

            // Создаем новые параметры с правильными типами и именами
            for (int i = 0; i < structure.ParameterTypes.Length; i++)
            {
                var parameterType = SyntaxFactory.ParseTypeName(structure.ParameterTypes[i]);
                var parameter = SyntaxFactory
                    .Parameter(SyntaxFactory.Identifier(structure.ParameterNames[i]))
                    .WithType(parameterType);
                parameters.Add(parameter);
            }

            // Создаем новый список параметров
            var parameterList = SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(parameters)
            );

            // Создаем новую декларацию метода с обновленными параметрами
            var newMethodDecl = methodDecl
                .WithParameterList(parameterList)
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
