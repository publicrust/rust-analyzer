using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer;

namespace OxideAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LangParameterAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OXD002";

        private static readonly LocalizableString Title =
            "Excessive language handling logic";

        private static readonly LocalizableString MessageFormat =
            "Excessive language handling logic detected. Simply use 'lang.GetMessage(key, this, userId)' to automatically handle language";

        private static readonly LocalizableString Description =
            "Oxide/uMod language system automatically handles language selection based on the user ID. There's no need for manual language management.";

        private const string Category = "API Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            
            // Проверяем, что это метод-обертка для локализации
            if (!IsLocalizationWrapperMethod(methodDeclaration))
                return;

            // Проверяем, что метод содержит логику для работы с языком
            if (ContainsUnnecessaryLanguageLogic(methodDeclaration))
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    methodDeclaration.Identifier.GetLocation()
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsLocalizationWrapperMethod(MethodDeclarationSyntax method)
        {
            // Проверяем имя метода
            if (method.Identifier.Text != "GetMessage" && 
                !method.Identifier.Text.Contains("Message") &&
                !method.Identifier.Text.Contains("Lang") &&
                !method.Identifier.Text.Contains("Localize"))
                return false;

            // Проверяем тип возвращаемого значения
            if (!(method.ReturnType is PredefinedTypeSyntax predefinedType && 
                 predefinedType.Keyword.ValueText == "string"))
                return false;

            // Проверяем, содержит ли тело метода вызов lang.GetMessage
            if (method.Body == null)
                return false;

            return method.Body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(i => IsLangGetMessageInvocation(i));
        }

        private bool ContainsUnnecessaryLanguageLogic(MethodDeclarationSyntax method)
        {
            if (method.Body == null)
                return false;

            // Ищем переменные с именами, похожими на обозначение языка
            var languageVariables = method.Body.DescendantNodes()
                .OfType<VariableDeclarationSyntax>()
                .Where(v => v.Variables.Any(var => 
                    var.Identifier.Text.Contains("lang") || 
                    var.Identifier.Text.Contains("Lang") ||
                    var.Identifier.Text.Contains("language") || 
                    var.Identifier.Text.Contains("Language")))
                .ToList();

            if (languageVariables.Count > 0)
                return true;

            // Ищем строковые литералы с кодами языков
            var languageLiterals = method.Body.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(l => l.Kind() == SyntaxKind.StringLiteralExpression)
                .Where(l => IsLanguageCode(l.Token.ValueText))
                .ToList();

            if (languageLiterals.Count > 0)
                return true;

            // Ищем вызовы методов получения языка
            var getLanguageCalls = method.Body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(i => {
                    if (i.Expression is MemberAccessExpressionSyntax memberAccess)
                    {
                        return memberAccess.Name.Identifier.Text == "GetLanguage";
                    }
                    return false;
                })
                .ToList();

            if (getLanguageCalls.Count > 0)
                return true;

            // Ищем вызовы lang.GetMessage с более чем 3 строками кода в методе
            var getMessageCalls = method.Body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsLangGetMessageInvocation)
                .ToList();

            var statementCount = method.Body.Statements.Count;
            
            // Если метод содержит более 1 оператора и заканчивается вызовом GetMessage,
            // это, вероятно, означает наличие избыточной логики
            return statementCount > 1 && getMessageCalls.Count > 0;
        }

        private bool IsLangGetMessageInvocation(InvocationExpressionSyntax invocation)
        {
            // Проверяем вызов вида: lang.GetMessage(...)
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name.Identifier.Text == "GetMessage" && 
                    (memberAccess.Expression.ToString() == "lang" || 
                     memberAccess.Expression.ToString().EndsWith(".lang")))
                {
                    return true;
                }
            }
            
            // Проверяем вызов вида: lang?.GetMessage(...)
            if (invocation.Expression is MemberBindingExpressionSyntax memberBinding)
            {
                if (memberBinding.Name.Identifier.Text == "GetMessage")
                {
                    // Получаем родительское выражение с условным доступом
                    var parent = invocation.Parent;
                    while (parent != null && !(parent is ConditionalAccessExpressionSyntax))
                    {
                        parent = parent.Parent;
                    }

                    if (parent is ConditionalAccessExpressionSyntax conditionalAccess &&
                        (conditionalAccess.Expression.ToString() == "lang" ||
                         conditionalAccess.Expression.ToString().EndsWith(".lang")))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsLanguageCode(string value)
        {
            // Список наиболее распространенных языковых кодов
            var commonLanguageCodes = new HashSet<string> { 
                "ru", "en", "fr", "de", "es", "it", "ja", "ko", "pt", "zh", "tr", "nl", "pl", "cs", "ar" 
            };

            // Проверяем, содержится ли значение в списке известных языковых кодов
            if (commonLanguageCodes.Contains(value.ToLowerInvariant()))
                return true;

            // Проверяем, соответствует ли значение паттерну языкового кода (2-3 символа)
            return value.Length >= 2 && value.Length <= 3 && value.All(char.IsLetter);
        }
    }
} 