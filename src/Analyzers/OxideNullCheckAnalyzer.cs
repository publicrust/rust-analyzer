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
    public class OxideNullCheckAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OXD001";

        private static readonly LocalizableString Title =
            "Object should be checked for null before use";

        private static readonly LocalizableString MessageFormat =
            "'{0}' at line {1} in hook {2} should be checked for null before use";

        private static readonly LocalizableString Description =
            "Objects in Oxide/uMod hooks should be checked for null before accessing their members to prevent NullReferenceException.";

        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
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
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
                return;

            if (
                !HooksConfiguration.IsKnownHook(methodSymbol)
                && !PluginHooksConfiguration.IsKnownHook(methodSymbol)
            )
                return;

            // Получаем полную сигнатуру хука для сообщения об ошибке
            var hookSignature =
                $"{methodSymbol.Name}({string.Join(", ", methodSymbol.Parameters.Select(p => $"{p.Type.Name} {p.Name}"))})";

            var parameters = methodDeclaration.ParameterList.Parameters;

            foreach (var parameter in parameters)
            {
                if (parameter.Type == null)
                    continue;

                var parameterType = context.SemanticModel.GetTypeInfo(parameter.Type);
                if (parameterType.Type == null || parameterType.Type.IsValueType)
                    continue;

                // Пропускаем строковые параметры, так как для них есть отдельный анализатор
                if (parameterType.Type.SpecialType == SpecialType.System_String)
                    continue;

                // Находим все обращения к параметру (методы и свойства)
                var memberAccesses = methodDeclaration
                    .DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Where(ma =>
                    {
                        // Проверяем, что это обращение к нашему параметру
                        if (
                            !(ma.Expression is IdentifierNameSyntax id)
                            || id.Identifier.Text != parameter.Identifier.Text
                        )
                            return false;

                        // Получаем символ члена
                        var memberSymbol = context.SemanticModel.GetSymbolInfo(ma).Symbol;

                        // Проверяем все члены (методы, свойства, поля)
                        return memberSymbol != null;
                    })
                    .ToList();

                // Собираем все проверки if до конца метода
                var allIfStatements = methodDeclaration
                    .DescendantNodes()
                    .OfType<IfStatementSyntax>()
                    .ToList();

                // Собираем все условные выражения с операторами && и ||
                var allBinaryExpressions = methodDeclaration
                    .DescendantNodes()
                    .OfType<BinaryExpressionSyntax>()
                    .Where(be => 
                        be.Kind() == SyntaxKind.LogicalAndExpression || 
                        be.Kind() == SyntaxKind.LogicalOrExpression)
                    .ToList();

                // Собираем все условные доступы (операторы ?.)
                var allConditionalAccesses = methodDeclaration
                    .DescendantNodes()
                    .OfType<ConditionalAccessExpressionSyntax>()
                    .ToList();

                foreach (var memberAccess in memberAccesses)
                {
                    var memberName = memberAccess.Name.Identifier.Text;
                    var lineNumber =
                        memberAccess.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                    // Проверяем, находится ли это обращение внутри проверки на null
                    var isInsideNullCheck = false;

                    // Проверяем, является ли родительское выражение условным доступом
                    if (IsPartOfConditionalAccess(memberAccess))
                    {
                        isInsideNullCheck = true;
                    }
                    else
                    {
                        // Проверяем все предыдущие условия
                        var parameterName = parameter.Identifier.Text;
                        
                        // Проверяем, защищено ли обращение предыдущими проверками на null
                        isInsideNullCheck = IsProtectedByPreviousNullChecks(
                            context,
                            memberAccess,
                            parameterName,
                            memberName,
                            allIfStatements,
                            allBinaryExpressions,
                            allConditionalAccesses
                        );
                    }

                    if (!isInsideNullCheck)
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule,
                            memberAccess.GetLocation(),
                            $"{parameter.Identifier.Text}.{memberName}",
                            lineNumber,
                            hookSignature
                        );
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private bool IsPartOfConditionalAccess(MemberAccessExpressionSyntax memberAccess)
        {
            // Проверяем, является ли memberAccess частью выражения с оператором ?.
            var parent = memberAccess.Parent;
            while (parent != null)
            {
                if (parent is ConditionalAccessExpressionSyntax conditionalAccess &&
                    conditionalAccess.Expression.ToString() == memberAccess.Expression.ToString())
                {
                    return true;
                }
                parent = parent.Parent;
            }
            return false;
        }

        private bool IsProtectedByPreviousNullChecks(
            SyntaxNodeAnalysisContext context,
            MemberAccessExpressionSyntax memberAccess,
            string parameterName,
            string memberName,
            IList<IfStatementSyntax> allIfStatements,
            IList<BinaryExpressionSyntax> allBinaryExpressions,
            IList<ConditionalAccessExpressionSyntax> allConditionalAccesses
        )
        {
            // 1. Проверяем, находится ли memberAccess внутри блока if с проверкой на null
            foreach (var ifStatement in allIfStatements)
            {
                // Проверяем, что memberAccess находится внутри блока if
                if (ifStatement.Statement.DescendantNodesAndSelf().Contains(memberAccess))
                {
                    // Проверяем условие if
                    if (ContainsNullCheck(ifStatement.Condition, parameterName, memberName))
                    {
                        return true;
                    }
                }
                
                // Проверяем, есть ли ранее проверка на null с return/continue/break
                if (ifStatement.SpanStart < memberAccess.SpanStart && 
                    IsNullCheckWithEarlyExit(ifStatement, parameterName))
                {
                    return true;
                }
            }

            // 2. Проверяем, защищено ли memberAccess предыдущими проверками на null с операторами && и ||
            SyntaxNode currentNode = memberAccess;
            while (currentNode != null)
            {
                if (currentNode is BinaryExpressionSyntax binaryExpression &&
                    binaryExpression.Kind() == SyntaxKind.LogicalAndExpression)
                {
                    // Проверяем, содержит ли левая часть && проверку на null
                    if (ContainsNullCheck(binaryExpression.Left, parameterName, memberName))
                    {
                        return true;
                    }
                }
                currentNode = currentNode.Parent;
            }

            // 3. Проверяем, защищено ли memberAccess предыдущими условными доступами
            foreach (var conditionalAccess in allConditionalAccesses)
            {
                if (conditionalAccess.SpanStart < memberAccess.SpanStart &&
                    conditionalAccess.Expression.ToString() == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsNullCheck(SyntaxNode condition, string parameterName, string memberName)
        {
            // Проверяем простые условия
            var conditionText = condition.ToString();
            if (conditionText.Contains($"{parameterName} != null") ||
                conditionText.Contains($"{parameterName} is not null") ||
                conditionText.Contains($"!({parameterName} is null)") ||
                conditionText.Contains($"!{parameterName}.IsNull()") ||
                conditionText.Contains($"{parameterName}.IsValid()") ||
                conditionText.Contains($"{parameterName}?.{memberName}") ||
                conditionText.Contains($"{parameterName}.{memberName} != null"))
            {
                return true;
            }

            // Рекурсивно проверяем сложные условия с операторами && и ||
            if (condition is BinaryExpressionSyntax binaryExpression)
            {
                if (binaryExpression.Kind() == SyntaxKind.LogicalAndExpression)
                {
                    // Для && достаточно, чтобы проверка была в левой части
                    return ContainsNullCheck(binaryExpression.Left, parameterName, memberName);
                }
                else if (binaryExpression.Kind() == SyntaxKind.LogicalOrExpression)
                {
                    // Для || нужно, чтобы проверка была в обеих частях
                    return ContainsNullCheck(binaryExpression.Left, parameterName, memberName) &&
                           ContainsNullCheck(binaryExpression.Right, parameterName, memberName);
                }
            }

            return false;
        }

        private bool IsNullCheckWithEarlyExit(IfStatementSyntax ifStatement, string parameterName)
        {
            // Проверяем, что условие проверяет параметр на null
            var condition = ifStatement.Condition.ToString();
            if (!(condition.Contains($"{parameterName} == null") || 
                  condition.Contains($"{parameterName} is null")))
            {
                return false;
            }

            // Проверяем, что в теле if есть return, continue или break
            if (ifStatement.Statement is BlockSyntax block)
            {
                return block.Statements.Any(stmt => 
                    stmt is ReturnStatementSyntax || 
                    stmt is ContinueStatementSyntax || 
                    stmt is BreakStatementSyntax);
            }
            else
            {
                return ifStatement.Statement is ReturnStatementSyntax || 
                       ifStatement.Statement is ContinueStatementSyntax || 
                       ifStatement.Statement is BreakStatementSyntax;
            }
        }
    }
}
