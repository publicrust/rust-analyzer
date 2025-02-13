using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.src.Configuration;
using RustAnalyzer.src.Models.StringPool;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StringPoolGetAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST0005";

        private static readonly string Title = "Invalid prefab name";
        private static readonly string MessageFormat =
            "String '{0}' does not exist in StringPool{1}";
        private static readonly string Description =
            "Prefab names must exist in StringPool when used with BaseNetworkable properties or methods that eventually call StringPool.Get(). This ensures runtime safety and prevents potential errors.";
        private const string Category = "Correctness";

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
            context.RegisterCompilationStartAction(InitializePerCompilation);
        }

        private void InitializePerCompilation(CompilationStartAnalysisContext context)
        {
            var compilationAnalyzer = new CompilationAnalyzer(context.Compilation);
            context.RegisterSyntaxNodeAction(
                compilationAnalyzer.AnalyzeBinaryExpression,
                SyntaxKind.EqualsExpression
            );
            context.RegisterSyntaxNodeAction(
                compilationAnalyzer.AnalyzeInvocationExpression,
                SyntaxKind.InvocationExpression
            );
        }

        private class CompilationAnalyzer
        {
            private readonly Compilation _compilation;
            private readonly Dictionary<IMethodSymbol, bool> _methodCallCache;

            public CompilationAnalyzer(Compilation compilation)
            {
                _compilation = compilation;
                _methodCallCache = new Dictionary<IMethodSymbol, bool>(
                    SymbolEqualityComparer.Default
                );
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

                    var invocations = methodDeclaration
                        .DescendantNodes()
                        .OfType<InvocationExpressionSyntax>();
                    foreach (var invocation in invocations)
                    {
                        var invokedMethodSymbol =
                            semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                        if (invokedMethodSymbol == null)
                            continue;

                        var methodConfig = StringPoolConfiguration.GetMethodConfig(
                            invokedMethodSymbol.ContainingType.Name,
                            invokedMethodSymbol.Name
                        );

                        if (methodConfig != null)
                        {
                            _methodCallCache[methodSymbol] = true;
                            return true;
                        }

                        if (
                            invokedMethodSymbol.MethodKind == MethodKind.Ordinary
                            && !invokedMethodSymbol.Equals(methodSymbol)
                        )
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

                if (IsGeneratedCode(binaryExpression.SyntaxTree, context.CancellationToken))
                {
                    return;
                }

                var leftMemberAccess = binaryExpression.Left as MemberAccessExpressionSyntax;
                var rightMemberAccess = binaryExpression.Right as MemberAccessExpressionSyntax;

                var leftLiteral = binaryExpression.Left as LiteralExpressionSyntax;
                var rightLiteral = binaryExpression.Right as LiteralExpressionSyntax;

                if (
                    !IsValidPrefabNameComparison(
                        context,
                        leftMemberAccess,
                        leftLiteral,
                        rightMemberAccess,
                        rightLiteral
                    )
                    && !IsValidPrefabNameComparison(
                        context,
                        rightMemberAccess,
                        rightLiteral,
                        leftMemberAccess,
                        leftLiteral
                    )
                )
                {
                    return;
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

                var methodConfig = StringPoolConfiguration.GetMethodConfig(
                    methodSymbol.ContainingType.Name,
                    methodSymbol.Name
                );

                if (methodConfig == null)
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

            private void AnalyzeArgument(
                SyntaxNodeAnalysisContext context,
                ExpressionSyntax argument,
                PrefabNameCheckType checkType
            )
            {
                var semanticModel = context.SemanticModel;

                if (argument is LiteralExpressionSyntax literal)
                {
                    if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                        return;

                    var stringValue = literal.Token.ValueText;
                    CheckStringValue(context, stringValue, literal.GetLocation(), checkType);
                }
                else
                {
                    var typeInfo = semanticModel.GetTypeInfo(argument);
                    if (
                        typeInfo.Type == null
                        || typeInfo.Type.SpecialType != SpecialType.System_String
                    )
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

            private void CheckStringValue(
                SyntaxNodeAnalysisContext context,
                string value,
                Location location,
                PrefabNameCheckType checkType
            )
            {
                if (checkType == PrefabNameCheckType.FullPath)
                {
                    if (!StringPoolConfiguration.IsValidPrefabPath(value))
                    {
                        var suggestions = StringPoolConfiguration.FindSimilarPrefabs(value);
                        string suggestionMessage = suggestions.Any()
                            ? $". Did you mean one of these:\n{string.Join("\n", suggestions.Select(s => $"  - {s}"))}"
                            : ". Make sure it starts with 'assets/prefabs/' and ends with '.prefab'";

                        ReportDiagnostic(context, location, value, suggestionMessage);
                    }
                }
                else
                {
                    if (!StringPoolConfiguration.IsValidShortName(value))
                    {
                        var suggestions = StringPoolConfiguration.FindSimilarShortNames(value);
                        string suggestionMessage = suggestions.Any()
                            ? $". Did you mean one of these: {string.Join(", ", suggestions)}?"
                            : ". Make sure to use a valid prefab short name";

                        ReportDiagnostic(context, location, value, suggestionMessage);
                    }
                }
            }

            private Dictionary<string, Location> GetStringLiteralsFromSymbol(
                ISymbol symbol,
                SemanticModel semanticModel
            )
            {
                var literals = new Dictionary<string, Location>(StringComparer.OrdinalIgnoreCase);

                foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
                {
                    var node = syntaxRef.GetSyntax();
                    if (
                        node is VariableDeclaratorSyntax variableDeclarator
                        && variableDeclarator.Initializer != null
                    )
                    {
                        var value = variableDeclarator.Initializer.Value;
                        if (
                            value is LiteralExpressionSyntax literal
                            && literal.IsKind(SyntaxKind.StringLiteralExpression)
                        )
                        {
                            literals[literal.Token.ValueText] = literal.GetLocation();
                        }
                    }
                    else if (node is ParameterSyntax parameterSyntax)
                    {
                        var parameterSymbol =
                            semanticModel.GetDeclaredSymbol(parameterSyntax) as IParameterSymbol;
                        if (parameterSymbol != null)
                        {
                            var methodSymbol = parameterSymbol.ContainingSymbol as IMethodSymbol;
                            if (methodSymbol != null)
                            {
                                var callers = FindMethodCallers(
                                    methodSymbol,
                                    semanticModel.Compilation
                                );
                                foreach (var caller in callers)
                                {
                                    if (
                                        caller.ArgumentList.Arguments.Count
                                        > parameterSymbol.Ordinal
                                    )
                                    {
                                        var arg = caller
                                            .ArgumentList
                                            .Arguments[parameterSymbol.Ordinal]
                                            .Expression;
                                        if (
                                            arg is LiteralExpressionSyntax argLiteral
                                            && argLiteral.IsKind(SyntaxKind.StringLiteralExpression)
                                        )
                                        {
                                            literals[argLiteral.Token.ValueText] =
                                                argLiteral.GetLocation();
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
                Compilation compilation
            )
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
                            return symbol != null
                                && SymbolEqualityComparer.Default.Equals(symbol, methodSymbol);
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
                LiteralExpressionSyntax? rightLiteral
            )
            {
                if (
                    (leftMember == null && rightMember == null)
                    || (leftLiteral == null && rightLiteral == null)
                )
                {
                    return false;
                }

                if (
                    (leftMember != null && rightMember != null)
                    || (leftLiteral != null && rightLiteral != null)
                )
                {
                    return false;
                }

                var memberAccess = leftMember ?? rightMember;

                if (memberAccess == null)
                {
                    return false;
                }

                var propertyName = memberAccess.Name.Identifier.Text;

                var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
                var objectType = typeInfo.Type;
                if (objectType == null)
                {
                    return false;
                }

                var currentType = objectType;
                while (currentType != null)
                {
                    var currentTypeName = currentType.ToDisplayString();

                    var propertyConfig = StringPoolConfiguration.GetPropertyConfig(
                        currentTypeName,
                        propertyName
                    );
                    if (propertyConfig == null)
                    {
                        propertyConfig = StringPoolConfiguration.GetPropertyConfig(
                            $"global::{currentTypeName}",
                            propertyName
                        );
                    }

                    if (propertyConfig != null)
                    {
                        var literal = leftLiteral ?? rightLiteral;
                        if (literal != null)
                        {
                            var value = literal.Token.ValueText;

                            CheckStringValue(
                                context,
                                value,
                                literal.GetLocation(),
                                propertyConfig.CheckType
                            );
                        }
                        return true;
                    }

                    currentType = currentType.BaseType;
                    if (currentType != null) { }
                }

                return false;
            }

            private void ReportDiagnostic(
                SyntaxNodeAnalysisContext context,
                Location location,
                string message,
                string suggestion
            )
            {
                var diagnostic = Diagnostic.Create(Rule, location, message, suggestion);

                context.ReportDiagnostic(diagnostic);
            }

            private bool IsGeneratedCode(SyntaxTree tree, CancellationToken cancellationToken)
            {
                if (tree == null)
                    throw new ArgumentNullException(nameof(tree));

                var root = tree.GetRoot(cancellationToken);
                foreach (var trivia in root.GetLeadingTrivia())
                {
                    if (
                        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    )
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
}
