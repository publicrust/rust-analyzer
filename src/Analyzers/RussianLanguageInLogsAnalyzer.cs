using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NonEnglishLanguageInLogsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RA0013";
        private const string Title = "Non-English language detected in logs";
        private const string MessageFormat = "Only English language is allowed in logs. Found non-English characters: '{0}'";
        private const string Description = "Using non-English characters in logs makes them harder to read and process. Use English only for better compatibility.";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            
            var methodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => null
            };

            if (methodName == null || !IsLoggingMethod(methodName))
                return;

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                CheckExpression(context, argument.Expression);
            }
        }

        private void CheckExpression(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            switch (expression)
            {
                case LiteralExpressionSyntax literal:
                    CheckText(context, literal.Token.ValueText, literal.GetLocation());
                    break;

                case InterpolatedStringExpressionSyntax interpolatedString:
                    foreach (var content in interpolatedString.Contents)
                    {
                        if (content is InterpolatedStringTextSyntax textPart)
                        {
                            CheckText(context, textPart.TextToken.ValueText, textPart.GetLocation());
                        }
                        else if (content is InterpolationSyntax interpolation)
                        {
                            // Рекурсивно проверяем выражения внутри интерполяции
                            CheckExpression(context, interpolation.Expression);
                        }
                    }
                    break;

                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                    // Проверяем конкатенацию строк
                    CheckExpression(context, binary.Left);
                    CheckExpression(context, binary.Right);
                    break;

                case InvocationExpressionSyntax invocation:
                    // Проверяем результаты вызовов методов, которые могут вернуть строку
                    foreach (var arg in invocation.ArgumentList.Arguments)
                    {
                        CheckExpression(context, arg.Expression);
                    }
                    break;
            }
        }

        private void CheckText(SyntaxNodeAnalysisContext context, string text, Location location)
        {
            var nonEnglishChars = GetNonEnglishCharacters(text);
            if (!string.IsNullOrEmpty(nonEnglishChars))
            {
                var diagnostic = Diagnostic.Create(Rule, location, nonEnglishChars);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsLoggingMethod(string methodName)
        {
            return methodName.Equals("Puts", StringComparison.OrdinalIgnoreCase) ||
                   methodName.Equals("Print", StringComparison.OrdinalIgnoreCase) ||
                   methodName.Equals("Log", StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("Log") ||
                   methodName.Contains("Console") ||
                   methodName.Contains("Debug") ||
                   methodName.Contains("Trace") ||
                   methodName.Contains("Info") ||
                   methodName.Contains("Error") ||
                   methodName.Contains("Warning");
        }

        private string GetNonEnglishCharacters(string text)
        {
            // Разрешаем:
            // - английские буквы (a-zA-Z)
            // - цифры (0-9)
            // - базовую пунктуацию и специальные символы
            // - пробелы, табуляции и новые строки
            var nonEnglishChars = text.Where(c => !IsAllowedCharacter(c)).Distinct().ToArray();
            return nonEnglishChars.Length > 0 ? new string(nonEnglishChars) : string.Empty;
        }

        private bool IsAllowedCharacter(char c)
        {
            // Разрешенные диапазоны символов:
            return (c >= 'a' && c <= 'z') || // Маленькие английские буквы
                   (c >= 'A' && c <= 'Z') || // Большие английские буквы
                   (c >= '0' && c <= '9') || // Цифры
                   char.IsWhiteSpace(c) ||   // Пробелы, табуляции, переносы строк
                   IsAllowedPunctuation(c);  // Разрешенная пунктуация
        }

        private bool IsAllowedPunctuation(char c)
        {
            // Разрешенные символы пунктуации и специальные символы
            return c == '.' || c == ',' || c == '!' || c == '?' || c == '-' ||
                   c == '_' || c == ':' || c == ';' || c == '(' || c == ')' ||
                   c == '[' || c == ']' || c == '{' || c == '}' || c == '/' ||
                   c == '\\' || c == '"' || c == '\'' || c == '+' || c == '=' ||
                   c == '<' || c == '>' || c == '@' || c == '#' || c == '$' ||
                   c == '%' || c == '^' || c == '&' || c == '*' || c == '|' ||
                   c == '~' || c == '`';
        }
    }
} 