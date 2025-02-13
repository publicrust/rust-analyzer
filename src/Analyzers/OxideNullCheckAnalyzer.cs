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
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
                return;

            if (
                !HooksConfiguration.IsHook(methodSymbol)
                && !PluginHooksConfiguration.IsHook(methodSymbol)
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

                foreach (var memberAccess in memberAccesses)
                {
                    var memberName = memberAccess.Name.Identifier.Text;
                    var lineNumber =
                        memberAccess.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                    // Проверяем, находится ли это обращение внутри проверки на null
                    var isInsideNullCheck = false;
                    var currentNode = memberAccess.Parent;

                    // Собираем все проверки if до текущего использования
                    var previousChecks = methodDeclaration
                        .DescendantNodes()
                        .OfType<IfStatementSyntax>()
                        .TakeWhile(ifStmt => ifStmt.SpanStart < memberAccess.SpanStart)
                        .Select(ifStmt => ifStmt.Condition.ToString())
                        .ToList();

                    // Проверяем все предыдущие условия
                    if (
                        previousChecks.Any(condition =>
                            condition.Contains($"{parameter.Identifier.Text} == null")
                            || condition.Contains($"{parameter.Identifier.Text}?.")
                            || condition.Contains($"{parameter.Identifier.Text} is null")
                            || condition.Contains($"!{parameter.Identifier.Text}.IsValid()")
                            || // Добавляем проверку IsValid()
                            condition.Contains($"{parameter.Identifier.Text}.{memberName} == null")
                            || condition.Contains($"{parameter.Identifier.Text}?.{memberName}")
                            || condition.Contains(
                                $"{parameter.Identifier.Text}.{memberName} is null"
                            )
                        )
                    )
                    {
                        isInsideNullCheck = true;
                    }
                    else
                    {
                        // Проверяем родительские if'ы
                        while (currentNode != null && currentNode != methodDeclaration)
                        {
                            if (currentNode is IfStatementSyntax ifStatement)
                            {
                                var condition = ifStatement.Condition.ToString();
                                if (
                                    condition.Contains($"{parameter.Identifier.Text} == null")
                                    || condition.Contains($"{parameter.Identifier.Text}?.")
                                    || condition.Contains($"{parameter.Identifier.Text} is null")
                                    || condition.Contains($"!{parameter.Identifier.Text}.IsValid()")
                                    || // Добавляем проверку IsValid()
                                    condition.Contains(
                                        $"{parameter.Identifier.Text}.{memberName} == null"
                                    )
                                    || condition.Contains(
                                        $"{parameter.Identifier.Text}?.{memberName}"
                                    )
                                    || condition.Contains(
                                        $"{parameter.Identifier.Text}.{memberName} is null"
                                    )
                                )
                                {
                                    isInsideNullCheck = true;
                                    break;
                                }
                            }
                            currentNode = currentNode.Parent;
                        }
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
    }
}
