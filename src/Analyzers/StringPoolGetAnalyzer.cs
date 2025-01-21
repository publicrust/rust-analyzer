using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer;
using RustAnalyzer.src.Configuration;
using RustAnalyzer.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StringPoolGetAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "RUST0005";

    private static readonly string[] PossibleBaseNetworkableNames = new[]
    {
        "BaseNetworkable",
        "global::BaseNetworkable"
    };

    private static readonly string Title = "Invalid prefab name";
    private static readonly string MessageFormat = "String '{0}' does not exist in StringPool{1}";
    private static readonly string Description = "Prefab names must exist in StringPool when used with BaseNetworkable properties or methods that eventually call StringPool.Get(). This ensures runtime safety and prevents potential errors.";

    private const string Category = "Correctness";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(InitializePerCompilation);
    }

    private void InitializePerCompilation(CompilationStartAnalysisContext context)
    {
        var compilationAnalyzer = new CompilationAnalyzer(context.Compilation);
        context.RegisterSyntaxNodeAction(compilationAnalyzer.AnalyzeBinaryExpression, SyntaxKind.EqualsExpression);
        context.RegisterSyntaxNodeAction(compilationAnalyzer.AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
    }

    private class CompilationAnalyzer
    {
        private readonly Compilation _compilation;
        private readonly Dictionary<(string TypeName, string MethodName), MethodConfig> _methodCache;
        private readonly Dictionary<IMethodSymbol, bool> _methodCallCache;

        private enum PrefabNameCheckType
        {
            FullPath,
            ShortName
        }

        private class MethodConfig
        {
            public string TypeName { get; }
            public string MethodName { get; }
            public List<int> ParameterIndices { get; }
            public PrefabNameCheckType CheckType { get; }

            public MethodConfig(string typeName, string methodName, List<int> parameterIndices, PrefabNameCheckType checkType = PrefabNameCheckType.FullPath)
            {
                TypeName = typeName;
                MethodName = methodName;
                ParameterIndices = parameterIndices;
                CheckType = checkType;
            }
        }

        public CompilationAnalyzer(Compilation compilation)
        {
            _compilation = compilation;
            _methodCache = new Dictionary<(string, string), MethodConfig>();
            _methodCallCache = new Dictionary<IMethodSymbol, bool>(SymbolEqualityComparer.Default);
            InitializeMethodCache();
        }

        private void InitializeMethodCache()
        {
            // Пример: Методы, работающие с полным путем префаба остаются без изменений,
            // а методы, которые работают с коротким именем — задаем CheckType = PrefabNameCheckType.ShortName
            var knownMethods = new List<MethodConfig>
            {
                new MethodConfig("GameManager", "CreateEntity", new List<int> { 0 }, PrefabNameCheckType.FullPath),
                new MethodConfig("BaseEntity", "Spawn", new List<int>(), PrefabNameCheckType.FullPath),
                new MethodConfig("PrefabAttribute", "server", new List<int>(), PrefabNameCheckType.FullPath),
                new MethodConfig("PrefabAttribute", "client", new List<int>(), PrefabNameCheckType.FullPath),
                new MethodConfig("GameManifest", "PathToStringID", new List<int> { 0 }, PrefabNameCheckType.FullPath),
                new MethodConfig("StringPool", "Add", new List<int> { 0 }, PrefabNameCheckType.FullPath),
                new MethodConfig("GameManager", "FindPrefab", new List<int> { 0 }, PrefabNameCheckType.FullPath),
                // Предположим, что CreateByName использует короткое имя
                new MethodConfig("ItemManager", "CreateByName", new List<int> { 0 }, PrefabNameCheckType.ShortName),
                // Предположим, что FindItemDefinition тоже использует короткое имя
                new MethodConfig("ItemManager", "FindItemDefinition", new List<int> { 0 }, PrefabNameCheckType.ShortName),
                new MethodConfig("GameManager", "LoadPrefab", new List<int> { 0 }, PrefabNameCheckType.FullPath),
                new MethodConfig("PrefabAttribute", "Find", new List<int> { 0 }, PrefabNameCheckType.FullPath),
                new MethodConfig("StringPool", "Get", new List<int> { 0 }, PrefabNameCheckType.FullPath)
            };

            foreach (var methodConfig in knownMethods)
            {
                var key = (methodConfig.TypeName, methodConfig.MethodName);
                _methodCache[key] = methodConfig;
            }

            foreach (var tree in _compilation.SyntaxTrees)
            {
                var semanticModel = _compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();

                var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var methodDeclaration in methodDeclarations)
                {
                    var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
                    if (methodSymbol == null)
                        continue;

                    var methodKey = (methodSymbol.ContainingType.Name, methodSymbol.Name);
                    if (_methodCache.ContainsKey(methodKey))
                        continue;

                    if (MethodCallsKnownMethod(methodSymbol))
                    {
                        var parameterIndices = Enumerable.Range(0, methodSymbol.Parameters.Length).ToList();
                        // По умолчанию пусть будет FullPath, при желании можно найти способ определить тип автоматически
                        var methodConfig = new MethodConfig(methodSymbol.ContainingType.Name, methodSymbol.Name, parameterIndices, PrefabNameCheckType.FullPath);
                        _methodCache[methodKey] = methodConfig;
                    }
                }
            }
        }

        private bool MethodCallsKnownMethod(IMethodSymbol methodSymbol)
        {
            if (_methodCallCache.TryGetValue(methodSymbol, out bool result))
            {
                return result;
            }

            _methodCallCache[methodSymbol] = false;

            foreach (var syntaxRef in methodSymbol.DeclaringSyntaxReferences)
            {
                var methodDeclaration = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
                if (methodDeclaration == null)
                    continue;

                var semanticModel = _compilation.GetSemanticModel(methodDeclaration.SyntaxTree);

                var invocations = methodDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    var invokedMethodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (invokedMethodSymbol == null)
                        continue;

                    var invokedMethodKey = (invokedMethodSymbol.ContainingType.Name, invokedMethodSymbol.Name);
                    if (_methodCache.ContainsKey(invokedMethodKey))
                    {
                        _methodCallCache[methodSymbol] = true;
                        return true;
                    }

                    if (invokedMethodSymbol.MethodKind == MethodKind.Ordinary && !invokedMethodSymbol.Equals(methodSymbol))
                    {
                        if (MethodCallsKnownMethod(invokedMethodSymbol))
                        {
                            _methodCallCache[methodSymbol] = true;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
        {
            var binaryExpression = (BinaryExpressionSyntax)context.Node;

            var leftMemberAccess = binaryExpression.Left as MemberAccessExpressionSyntax;
            var rightMemberAccess = binaryExpression.Right as MemberAccessExpressionSyntax;

            var leftLiteral = binaryExpression.Left as LiteralExpressionSyntax;
            var rightLiteral = binaryExpression.Right as LiteralExpressionSyntax;

            if (!IsValidPrefabNameComparison(context, leftMemberAccess, leftLiteral, rightMemberAccess, rightLiteral))
            {
                return;
            }

            var literalExpression = leftLiteral ?? rightLiteral;
            var memberAccess = leftMemberAccess ?? rightMemberAccess;

            // Если не строковый литерал, выходим
            if (literalExpression == null || memberAccess == null || !literalExpression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return;
            }

            var stringValue = literalExpression.Token.ValueText;
            bool isShortPrefabName = memberAccess.Name.Identifier.Text == "ShortPrefabName";

            if (isShortPrefabName)
            {
                if (!IsValidShortName(stringValue))
                {
                    var suggestions = FindSimilarShortNames(stringValue);

                    string suggestionMessage = suggestions.Any()
                        ? $" Did you mean one of these: {string.Join(", ", suggestions)}?"
                        : " Make sure to use a valid prefab short name";

                    var diagnostic = Diagnostic.Create(
                        Rule,
                        literalExpression.GetLocation(),
                        stringValue,
                        suggestionMessage);
                    context.ReportDiagnostic(diagnostic);
                }
            }
            else
            {
                if (!IsValidPrefabPath(stringValue))
                {
                    var suggestions = FindSimilarPrefabs(stringValue);

                    string suggestionMessage = suggestions.Any()
                        ? $" Did you mean one of these: {string.Join(", ", suggestions)}?"
                        : " Make sure to use a valid prefab path";

                    var diagnostic = Diagnostic.Create(
                        Rule,
                        literalExpression.GetLocation(),
                        stringValue,
                        suggestionMessage);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        public void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            if (IsGeneratedCode(invocation.SyntaxTree, context.CancellationToken))
                return;

            var semanticModel = context.SemanticModel;
            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol == null)
                return;

            var methodKey = (methodSymbol.ContainingType.Name, methodSymbol.Name);

            if (!_methodCache.TryGetValue(methodKey, out var methodConfig))
                return;

            var arguments = invocation.ArgumentList.Arguments;
            foreach (var index in methodConfig.ParameterIndices)
            {
                if (arguments.Count > index)
                {
                    var argument = arguments[index].Expression;
                    AnalyzeArgument(context, argument, methodConfig.CheckType);
                }
            }
        }

        private void AnalyzeArgument(SyntaxNodeAnalysisContext context, ExpressionSyntax argument, PrefabNameCheckType checkType)
        {
            var semanticModel = context.SemanticModel;

            if (argument is LiteralExpressionSyntax literal)
            {
                // Проверяем, что литерал строковый
                if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                    return;

                var stringValue = literal.Token.ValueText;
                CheckStringValue(context, stringValue, literal.GetLocation(), checkType);
            }
            else
            {
                // Проверим, что тип аргумента - строка
                var typeInfo = semanticModel.GetTypeInfo(argument);
                if (typeInfo.Type == null || typeInfo.Type.SpecialType != SpecialType.System_String)
                    return;

                var symbol = semanticModel.GetSymbolInfo(argument).Symbol;
                if (symbol != null)
                {
                    var literals = GetStringLiteralsFromSymbol(symbol, semanticModel);
                    foreach (var kvp in literals)
                    {
                        CheckStringValue(context, kvp.Key, kvp.Value, checkType);
                    }
                }
            }
        }

        private void CheckStringValue(SyntaxNodeAnalysisContext context, string value, Location location, PrefabNameCheckType checkType)
        {
            if (checkType == PrefabNameCheckType.FullPath)
            {
                if (!IsValidPrefabPath(value))
                {
                    ReportDiagnostic(
                        context,
                        location,
                        value,
                        GetSuggestionMessage(value)
                    );
                }
            }
            else
            {
                // Проверяем короткое имя
                if (!IsValidShortName(value))
                {
                    var suggestions = FindSimilarShortNames(value);
                    string suggestionMessage = suggestions.Any()
                        ? $" Did you mean one of these: {string.Join(", ", suggestions)}?"
                        : " Make sure to use a valid prefab short name";

                    ReportDiagnostic(
                        context,
                        location,
                        value,
                        suggestionMessage
                    );
                }
            }
        }

        private bool IsValidShortName(string shortName)
        {
            shortName = shortName.ToLowerInvariant().Trim();
            foreach (var prefabName in StringPoolConfiguration.ToNumber.Keys)
            {
                var sn = Path.GetFileNameWithoutExtension(prefabName);
                if (sn.Equals(shortName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsValidPrefabPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            path = path.ToLowerInvariant().Replace("\\", "/").Trim();
            return StringPoolConfiguration.ToNumber.ContainsKey(path);
        }

        private string GetSuggestionMessage(string invalidPath)
        {
            var suggestions = FindSimilarPrefabs(invalidPath);

            if (suggestions.Any())
            {
                return $" Invalid prefab path. Did you mean one of these?\n" +
                       string.Join("\n", suggestions.Select(s => $"  - {s}"));
            }

            return " Invalid prefab path. Make sure it starts with 'assets/prefabs/' and ends with '.prefab'";
        }

        private IEnumerable<string> FindSimilarPrefabs(string invalidPath)
        {
            invalidPath = invalidPath.ToLowerInvariant().Replace("\\", "/").Trim();

            return StringPoolConfiguration.ToNumber.Keys
                .Select(p => new { Path = p, Distance = StringDistance.GetLevenshteinDistance(p, invalidPath) })
                .Where(x => x.Distance <= 5)
                .OrderBy(x => x.Distance)
                .Take(3)
                .Select(x => x.Path);
        }

        private IEnumerable<string> FindSimilarShortNames(string shortName)
        {
            shortName = shortName.ToLowerInvariant();

            return StringPoolConfiguration.ToNumber.Keys
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .Distinct()
                .Select(sn => new { ShortName = sn, Distance = StringDistance.GetLevenshteinDistance(sn, shortName) })
                .Where(x => x.Distance <= 3)
                .OrderBy(x => x.Distance)
                .Take(3)
                .Select(x => x.ShortName);
        }

        private Dictionary<string, Location> GetStringLiteralsFromSymbol(ISymbol symbol, SemanticModel semanticModel)
        {
            var literals = new Dictionary<string, Location>(StringComparer.OrdinalIgnoreCase);

            foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
            {
                var node = syntaxRef.GetSyntax();
                if (node is VariableDeclaratorSyntax variableDeclarator && variableDeclarator.Initializer != null)
                {
                    var value = variableDeclarator.Initializer.Value;
                    if (value is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        literals[literal.Token.ValueText] = literal.GetLocation();
                    }
                }
                else if (node is ParameterSyntax parameterSyntax)
                {
                    var parameterSymbol = semanticModel.GetDeclaredSymbol(parameterSyntax) as IParameterSymbol;
                    if (parameterSymbol != null)
                    {
                        var methodSymbol = parameterSymbol.ContainingSymbol as IMethodSymbol;
                        if (methodSymbol != null)
                        {
                            var callers = FindMethodCallers(methodSymbol, semanticModel.Compilation);
                            foreach (var caller in callers)
                            {
                                if (caller.ArgumentList.Arguments.Count > parameterSymbol.Ordinal)
                                {
                                    var arg = caller.ArgumentList.Arguments[parameterSymbol.Ordinal].Expression;
                                    if (arg is LiteralExpressionSyntax argLiteral && argLiteral.IsKind(SyntaxKind.StringLiteralExpression))
                                    {
                                        literals[argLiteral.Token.ValueText] = argLiteral.GetLocation();
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return literals;
        }

        private IEnumerable<InvocationExpressionSyntax> FindMethodCallers(
            IMethodSymbol methodSymbol,
            Compilation compilation)
        {
            var callers = new List<InvocationExpressionSyntax>();

            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();

                var invocations = root.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation =>
                    {
                        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                        var symbol = symbolInfo.Symbol;
                        return symbol != null && SymbolEqualityComparer.Default.Equals(symbol, methodSymbol);
                    });

                callers.AddRange(invocations);
            }

            return callers;
        }

        private bool IsValidPrefabNameComparison(
            SyntaxNodeAnalysisContext context,
            MemberAccessExpressionSyntax? leftMember,
            LiteralExpressionSyntax? leftLiteral,
            MemberAccessExpressionSyntax? rightMember,
            LiteralExpressionSyntax? rightLiteral)
        {
            if ((leftMember == null && rightMember == null) || (leftLiteral == null && rightLiteral == null))
                return false;

            if ((leftMember != null && rightMember != null) || (leftLiteral != null && rightLiteral != null))
                return false;

            var memberAccess = leftMember ?? rightMember;

            if (memberAccess == null)
                return false;

            var propertyName = memberAccess.Name.Identifier.Text;

            if (!(propertyName == "PrefabName" || propertyName == "ShortPrefabName"))
                return false;

            var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
            var objectType = typeInfo.Type;
            if (objectType == null)
                return false;

            var currentType = objectType;
            while (currentType != null)
            {
                var currentTypeName = currentType.ToDisplayString();

                if (PossibleBaseNetworkableNames.Contains(currentTypeName))
                    return true;

                currentType = currentType.BaseType;
            }

            return false;
        }

        private void ReportDiagnostic(SyntaxNodeAnalysisContext context, Location location, string message, string suggestion)
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                location,
                message,
                suggestion);

            context.ReportDiagnostic(diagnostic);
        }

        private bool IsGeneratedCode(SyntaxTree tree, CancellationToken cancellationToken)
        {
            if (tree == null)
                throw new ArgumentNullException(nameof(tree));

            var root = tree.GetRoot(cancellationToken);
            foreach (var trivia in root.GetLeadingTrivia())
            {
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                    trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                {
                    var text = trivia.ToFullString();
                    if (text.Contains("<auto-generated"))
                        return true;
                }
            }
            return false;
        }
    }
}
