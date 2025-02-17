using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Utils;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PluginReferenceDependencyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RA0050";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Missing plugin dependency check";

        private static readonly string NoteTemplate =
            "Field '{0}' with [PluginReference] attribute must be checked for null in OnServerInitialized method";

        private static readonly string HelpTemplate =
            "Add a null check at the start of OnServerInitialized method:\n"
            + "if ({0} == null)\n"
            + "{\n"
            + "    PrintError(\"{0} plugin is not installed!\");\n"
            + "    NextTick(() => Interface.Oxide.UnloadPlugin(Name));\n"
            + "    return;\n"
            + "}";

        private static readonly string ExampleTemplate =
            "private void OnServerInitialized(bool initial)\n"
            + "{\n"
            + "    if ({0} == null)\n"
            + "    {\n"
            + "        PrintError(\"{0} plugin is not installed!\");\n"
            + "    }\n"
            + "}";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: Title,
            messageFormat: "Field '{0}' with [PluginReference] attribute must be checked for null in OnServerInitialized method",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Plugin references should be checked for null in OnServerInitialized to handle missing dependencies.",
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RA0050.md"
        );

        private static readonly SymbolEqualityComparer SymbolComparer =
            SymbolEqualityComparer.Default;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Регистрируем анализ для всего класса
            context.RegisterSymbolAction(AnalyzeClass, SymbolKind.NamedType);
        }

        private void AnalyzeClass(SymbolAnalysisContext context)
        {
            var classSymbol = (INamedTypeSymbol)context.Symbol;

            System.Console.WriteLine($"[DEBUG] Analyzing class: {classSymbol.Name}");

            // Получаем все поля с [PluginReference]
            var pluginReferenceFields = GetPluginReferenceFields(classSymbol).ToList();
            System.Console.WriteLine(
                $"[DEBUG] Found {pluginReferenceFields.Count} plugin reference fields:"
            );
            foreach (var field in pluginReferenceFields)
            {
                System.Console.WriteLine($"[DEBUG] - {field.Name}");
            }

            if (!pluginReferenceFields.Any())
                return;

            // Ищем метод OnServerInitialized
            var onServerInitMethod = classSymbol
                .GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Name == "OnServerInitialized");

            if (onServerInitMethod == null)
            {
                System.Console.WriteLine("[DEBUG] OnServerInitialized method not found");
                foreach (var field in pluginReferenceFields)
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        field.Locations[0],
                        ImmutableDictionary<string, string>.Empty.Add("FieldName", field.Name),
                        field.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
                return;
            }

            // Получаем синтаксис метода
            var methodSyntax =
                onServerInitMethod.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()
                as MethodDeclarationSyntax;

            if (methodSyntax == null || methodSyntax.Body == null)
            {
                System.Console.WriteLine("[DEBUG] Method body is null");
                foreach (var field in pluginReferenceFields)
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        field.Locations[0],
                        ImmutableDictionary<string, string>.Empty.Add("FieldName", field.Name),
                        field.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
                return;
            }

            var semanticModel = context.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
            var nullChecks = GetNullChecks(methodSyntax, semanticModel);

            System.Console.WriteLine($"[DEBUG] Found {nullChecks.Count} null checks in method");

            // Проверяем каждое поле с [PluginReference]
            var uncheckedFields = new List<IFieldSymbol>();
            foreach (var field in pluginReferenceFields)
            {
                if (!IsFieldCheckedForNull(field, nullChecks))
                {
                    System.Console.WriteLine($"[DEBUG] Field {field.Name} is not checked for null");
                    uncheckedFields.Add(field);
                }
                else
                {
                    System.Console.WriteLine($"[DEBUG] Field {field.Name} has null check");
                }
            }

            if (uncheckedFields.Any())
            {
                System.Console.WriteLine(
                    $"[DEBUG] Creating diagnostics for {uncheckedFields.Count} fields"
                );
                var properties = ImmutableDictionary<string, string>.Empty.Add(
                    "FieldNames",
                    string.Join(",", uncheckedFields.Select(f => f.Name))
                );

                foreach (var field in uncheckedFields)
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        field.Locations[0],
                        properties,
                        field.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void ReportEmptyMethodError(
            SyntaxNodeAnalysisContext context,
            MethodDeclarationSyntax method
        )
        {
            var location = method.Identifier.GetLocation();
            var sourceText = location.SourceTree?.GetText();
            if (sourceText == null)
                return;

            var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
            {
                ErrorCode = DiagnosticId,
                ErrorTitle = "Empty OnServerInitialized method",
                Location = location,
                SourceText = sourceText,
                MessageParameters = new[] { method.Identifier.Text },
                Note = "OnServerInitialized method must contain plugin dependency checks",
                Help = "Add null checks for all plugin references",
                Example = ExampleTemplate,
            };

            var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
            var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
            context.ReportDiagnostic(diagnostic);
        }

        private void ReportMissingNullCheck(SyntaxNodeAnalysisContext context, IFieldSymbol field)
        {
            var location = field.Locations[0];
            var sourceText = location.SourceTree?.GetText();
            if (sourceText == null)
                return;

            var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
            {
                ErrorCode = DiagnosticId,
                ErrorTitle = "Missing plugin dependency check",
                Location = location,
                SourceText = sourceText,
                MessageParameters = new[] { field.Name },
                Note = string.Format(NoteTemplate, field.Name),
                Help = string.Format(HelpTemplate, field.Name),
                Example = string.Format(ExampleTemplate, field.Name),
            };

            var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
            var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
            context.ReportDiagnostic(diagnostic);
        }

        private void ReportIncorrectNullCheckLocation(
            SyntaxNodeAnalysisContext context,
            IFieldSymbol field
        )
        {
            var location = field.Locations.First();
            var sourceText = location.SourceTree?.GetText();
            if (sourceText == null)
                return;

            var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
            {
                ErrorCode = DiagnosticId,
                ErrorTitle = "Incorrect null check location",
                Location = location,
                SourceText = sourceText,
                MessageParameters = new[] { field.Name },
                Note =
                    $"Null check for field '{field.Name}' must be at the start of OnServerInitialized method",
                Help = "Move the null check to the beginning of the method",
                Example = string.Format(ExampleTemplate, field.Name),
            };

            var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
            var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
            context.ReportDiagnostic(diagnostic);
        }

        private void ReportImproperNullHandling(
            SyntaxNodeAnalysisContext context,
            IFieldSymbol field
        )
        {
            var location = field.Locations.First();
            var sourceText = location.SourceTree?.GetText();
            if (sourceText == null)
                return;

            var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
            {
                ErrorCode = DiagnosticId,
                ErrorTitle = "Improper null check handling",
                Location = location,
                SourceText = sourceText,
                MessageParameters = new[] { field.Name },
                Note = $"Null check for field '{field.Name}' must properly handle missing plugin",
                Help = "Add PrintError and UnloadPlugin calls inside the null check",
                Example = string.Format(ExampleTemplate, field.Name),
            };

            var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
            var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
            context.ReportDiagnostic(diagnostic);
        }

        private bool IsNullCheckAtMethodStart(
            MethodDeclarationSyntax method,
            IFieldSymbol field,
            SemanticModel semanticModel
        )
        {
            // Проверяем первые statements в методе
            var statements = method.Body?.Statements.Take(5).ToList();
            if (statements == null || !statements.Any())
                return false;

            foreach (var statement in statements)
            {
                if (statement is IfStatementSyntax ifStatement)
                {
                    if (IsNullCheckForField(ifStatement.Condition, field, semanticModel))
                        return true;
                }
            }

            return false;
        }

        private bool IsNullCheckForField(
            ExpressionSyntax expression,
            IFieldSymbol field,
            SemanticModel semanticModel
        )
        {
            if (expression is BinaryExpressionSyntax binaryExpression)
            {
                if (IsNullComparison(binaryExpression))
                {
                    var nonNullSide =
                        binaryExpression.Left is LiteralExpressionSyntax
                            ? binaryExpression.Right
                            : binaryExpression.Left;
                    var symbol = semanticModel.GetSymbolInfo(nonNullSide).Symbol;
                    return SymbolComparer.Equals(symbol, field);
                }
            }
            return false;
        }

        private bool HasProperNullHandling(
            MethodDeclarationSyntax method,
            IFieldSymbol field,
            SemanticModel semanticModel
        )
        {
            // Ищем if-statement с проверкой на null
            var statements = method.Body?.Statements.Take(5).ToList();
            if (statements == null || !statements.Any())
                return false;

            foreach (var statement in statements)
            {
                if (statement is IfStatementSyntax ifStatement)
                {
                    if (IsNullCheckForField(ifStatement.Condition, field, semanticModel))
                    {
                        // Проверяем наличие вызова PrintError и UnloadPlugin
                        var block = ifStatement.Statement as BlockSyntax;
                        if (block == null)
                            return false;

                        bool hasPrintError = false;
                        bool hasUnloadPlugin = false;

                        foreach (var blockStatement in block.Statements)
                        {
                            if (blockStatement is ExpressionStatementSyntax exprStatement)
                            {
                                if (
                                    exprStatement.Expression
                                    is InvocationExpressionSyntax invocation
                                )
                                {
                                    var methodSymbol =
                                        semanticModel.GetSymbolInfo(invocation.Expression).Symbol
                                        as IMethodSymbol;
                                    if (methodSymbol == null)
                                        continue;

                                    if (methodSymbol.Name == "PrintError")
                                        hasPrintError = true;
                                    else if (
                                        methodSymbol.Name == "UnloadPlugin"
                                        || (
                                            methodSymbol.Name == "NextTick"
                                            && invocation.ToString().Contains("UnloadPlugin")
                                        )
                                    )
                                        hasUnloadPlugin = true;
                                }
                            }
                        }

                        return hasPrintError && hasUnloadPlugin;
                    }
                }
            }

            return false;
        }

        private IEnumerable<IFieldSymbol> GetPluginReferenceFields(INamedTypeSymbol classSymbol)
        {
            var fields = new List<IFieldSymbol>();

            // Получаем все объявления полей
            var declarations = classSymbol
                .DeclaringSyntaxReferences.Select(r => r.GetSyntax())
                .OfType<ClassDeclarationSyntax>()
                .SelectMany(c => c.Members)
                .OfType<FieldDeclarationSyntax>();

            foreach (var declaration in declarations)
            {
                // Проверяем наличие атрибута PluginReference
                var hasPluginRef = declaration
                    .AttributeLists.SelectMany(al => al.Attributes)
                    .Any(attr =>
                    {
                        var name = attr.Name.ToString();
                        return name == "PluginReference" || name == "PluginReferenceAttribute";
                    });

                if (hasPluginRef)
                {
                    // Для каждого объявленного поля получаем его символ
                    foreach (var variable in declaration.Declaration.Variables)
                    {
                        var symbol = classSymbol
                            .GetMembers(variable.Identifier.Text)
                            .OfType<IFieldSymbol>()
                            .FirstOrDefault();

                        if (symbol != null)
                        {
                            fields.Add(symbol);
                        }
                    }
                }
            }

            return fields;
        }

        private HashSet<ISymbol> GetNullChecks(
            MethodDeclarationSyntax method,
            SemanticModel semanticModel
        )
        {
            var nullChecks = new HashSet<ISymbol>(SymbolComparer);

            // Ищем все проверки на null в методе
            var binaryExpressions = method.DescendantNodes().OfType<BinaryExpressionSyntax>();

            System.Console.WriteLine(
                $"[PluginDependencyAnalyzer] Found {binaryExpressions.Count()} total binary expressions"
            );

            foreach (var expression in binaryExpressions)
            {
                System.Console.WriteLine(
                    $"[PluginDependencyAnalyzer] Checking binary expression: {expression} of kind {expression.Kind()}"
                );

                // Проверяем сложные логические выражения
                if (
                    expression.OperatorToken.IsKind(SyntaxKind.BarBarToken)
                    || expression.OperatorToken.IsKind(SyntaxKind.AmpersandAmpersandToken)
                )
                {
                    System.Console.WriteLine(
                        $"[PluginDependencyAnalyzer] Found logical expression: {expression}"
                    );
                    var symbols = GetSymbolsFromNullChecks(expression, semanticModel);
                    foreach (var symbol in symbols)
                    {
                        if (symbol != null)
                        {
                            System.Console.WriteLine(
                                $"[PluginDependencyAnalyzer] Found null check in logical expression for: {symbol.Name}"
                            );
                            nullChecks.Add(symbol);
                        }
                    }
                }
                // Проверяем простые сравнения с null
                else if (expression.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken))
                {
                    System.Console.WriteLine(
                        $"[PluginDependencyAnalyzer] Found equality check: {expression}"
                    );
                    if (IsNullComparison(expression))
                    {
                        var nonNullSide =
                            expression.Left is LiteralExpressionSyntax
                                ? expression.Right
                                : expression.Left;
                        var symbol = semanticModel.GetSymbolInfo(nonNullSide).Symbol;
                        if (symbol != null)
                        {
                            System.Console.WriteLine(
                                $"[PluginDependencyAnalyzer] Found null check for: {symbol.Name} ({symbol.Kind})"
                            );
                            nullChecks.Add(symbol);
                        }
                    }
                }
            }

            // Также ищем проверки через is null pattern
            var isPatternExpressions = method.DescendantNodes().OfType<IsPatternExpressionSyntax>();

            System.Console.WriteLine(
                $"[PluginDependencyAnalyzer] Found {isPatternExpressions.Count()} is pattern expressions"
            );

            foreach (var isPattern in isPatternExpressions)
            {
                System.Console.WriteLine(
                    $"[PluginDependencyAnalyzer] Checking is pattern: {isPattern}"
                );
                if (
                    isPattern.Pattern is ConstantPatternSyntax cp
                    && cp.Expression is LiteralExpressionSyntax les
                    && les.IsKind(SyntaxKind.NullLiteralExpression)
                )
                {
                    var symbol = semanticModel.GetSymbolInfo(isPattern.Expression).Symbol;
                    if (symbol != null)
                    {
                        System.Console.WriteLine(
                            $"[PluginDependencyAnalyzer] Found null check (is pattern) for: {symbol.Name} ({symbol.Kind})"
                        );
                        nullChecks.Add(symbol);
                    }
                }
            }

            return nullChecks;
        }

        private IEnumerable<ISymbol> GetSymbolsFromNullChecks(
            BinaryExpressionSyntax expression,
            SemanticModel semanticModel
        )
        {
            var symbols = new List<ISymbol>();

            System.Console.WriteLine(
                $"[PluginDependencyAnalyzer] Analyzing complex expression: {expression}"
            );

            // Рекурсивно проверяем левую часть
            if (expression.Left is BinaryExpressionSyntax leftBinary)
            {
                System.Console.WriteLine(
                    $"[PluginDependencyAnalyzer] Recursing into left part: {leftBinary}"
                );
                symbols.AddRange(GetSymbolsFromNullChecks(leftBinary, semanticModel));
            }
            else if (
                expression.Left is BinaryExpressionSyntax leftNullCheck
                && IsNullComparison(leftNullCheck)
            )
            {
                System.Console.WriteLine(
                    $"[PluginDependencyAnalyzer] Found null check in left part: {leftNullCheck}"
                );
                var nonNullSide =
                    leftNullCheck.Left is LiteralExpressionSyntax
                        ? leftNullCheck.Right
                        : leftNullCheck.Left;
                var symbol = semanticModel.GetSymbolInfo(nonNullSide).Symbol;
                if (symbol != null)
                {
                    System.Console.WriteLine(
                        $"[PluginDependencyAnalyzer] Found symbol in left part: {symbol.Name}"
                    );
                    symbols.Add(symbol);
                }
            }

            // Рекурсивно проверяем правую часть
            if (expression.Right is BinaryExpressionSyntax rightBinary)
            {
                System.Console.WriteLine(
                    $"[PluginDependencyAnalyzer] Recursing into right part: {rightBinary}"
                );
                symbols.AddRange(GetSymbolsFromNullChecks(rightBinary, semanticModel));
            }
            else if (
                expression.Right is BinaryExpressionSyntax rightNullCheck
                && IsNullComparison(rightNullCheck)
            )
            {
                System.Console.WriteLine(
                    $"[PluginDependencyAnalyzer] Found null check in right part: {rightNullCheck}"
                );
                var nonNullSide =
                    rightNullCheck.Left is LiteralExpressionSyntax
                        ? rightNullCheck.Right
                        : rightNullCheck.Left;
                var symbol = semanticModel.GetSymbolInfo(nonNullSide).Symbol;
                if (symbol != null)
                {
                    System.Console.WriteLine(
                        $"[PluginDependencyAnalyzer] Found symbol in right part: {symbol.Name}"
                    );
                    symbols.Add(symbol);
                }
            }

            return symbols;
        }

        private bool IsNullComparison(ExpressionSyntax expression)
        {
            Console.WriteLine(
                $"[RustAnalyzer] Checking expression for null comparison: {expression.Kind()} - {expression}"
            );

            // Проверяем прямое сравнение с null (x == null или null == x)
            if (expression is BinaryExpressionSyntax binaryExpression)
            {
                if (binaryExpression.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken))
                {
                    var isLeftNull = binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression);
                    var isRightNull = binaryExpression.Right.IsKind(
                        SyntaxKind.NullLiteralExpression
                    );

                    Console.WriteLine($"[RustAnalyzer] Binary expression: {binaryExpression}");
                    Console.WriteLine(
                        $"[RustAnalyzer] Left is null: {isLeftNull}, Right is null: {isRightNull}"
                    );

                    return isLeftNull || isRightNull;
                }
            }

            // Проверяем паттерн is null
            if (expression is IsPatternExpressionSyntax isPattern)
            {
                if (
                    isPattern.Pattern is ConstantPatternSyntax constantPattern
                    && constantPattern.Expression.IsKind(SyntaxKind.NullLiteralExpression)
                )
                {
                    Console.WriteLine($"[RustAnalyzer] Is pattern expression: {isPattern}");
                    return true;
                }
            }

            // Проверяем унарное отрицание (!x)
            if (
                expression is PrefixUnaryExpressionSyntax prefixUnary
                && prefixUnary.OperatorToken.IsKind(SyntaxKind.ExclamationToken)
            )
            {
                Console.WriteLine($"[RustAnalyzer] Prefix unary expression: {prefixUnary}");
                return true;
            }

            // Проверяем приведение к bool (if(x) или if(!x))
            if (expression is IdentifierNameSyntax)
            {
                Console.WriteLine($"[RustAnalyzer] Identifier name expression: {expression}");
                return true;
            }

            Console.WriteLine($"[RustAnalyzer] Expression is not a null check: {expression}");
            return false;
        }

        private bool IsFieldCheckedForNull(IFieldSymbol field, HashSet<ISymbol> nullChecks)
        {
            System.Console.WriteLine(
                $"[PluginDependencyAnalyzer] Checking if field {field.Name} is checked for null"
            );
            System.Console.WriteLine($"[PluginDependencyAnalyzer] Field type: {field.Type}");
            System.Console.WriteLine(
                $"[PluginDependencyAnalyzer] Field containing type: {field.ContainingType}"
            );

            foreach (var check in nullChecks)
            {
                System.Console.WriteLine(
                    $"[PluginDependencyAnalyzer] Comparing with null check: {check.Name} ({check.Kind})"
                );
                System.Console.WriteLine(
                    $"[PluginDependencyAnalyzer] Check type: {check.GetType()}"
                );
                System.Console.WriteLine(
                    $"[PluginDependencyAnalyzer] Check containing type: {check.ContainingType}"
                );

                if (SymbolComparer.Equals(check, field))
                {
                    System.Console.WriteLine(
                        $"[PluginDependencyAnalyzer] Found matching null check for field {field.Name}"
                    );
                    return true;
                }
            }

            System.Console.WriteLine(
                $"[PluginDependencyAnalyzer] No matching null check found for field {field.Name}"
            );
            return false;
        }
    }
}
