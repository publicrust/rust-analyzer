using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StringNullCheckAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST007";
        private const string Category = "Safety";

        private static readonly LocalizableString Title = "String null check may be required";
        private static readonly LocalizableString MessageFormat = "'{0}' should be checked for null before use";
        private static readonly LocalizableString Description = "String methods should only be called after ensuring the string is not null.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // Do not analyze generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;

            // Check that the invocation is on an instance of type string.
            if (invocation.Instance?.Type == null || invocation.Instance.Type.SpecialType != SpecialType.System_String)
            {
                return;
            }

            // Get the syntax node for the instance on which the method is called.
            var instanceSyntax = invocation.Instance.Syntax;
            if (instanceSyntax == null)
            {
                return;
            }

            // Debug logging.
            Console.WriteLine($"\n[StringNullCheckAnalyzer] Analyzing string method call: {instanceSyntax}");

            // If the instance expression is a literal or constant, skip diagnostic.
            if (IsConstantOrLiteral(instanceSyntax, context))
            {
                Console.WriteLine("[StringNullCheckAnalyzer] Expression is a constant or literal, skipping diagnostic.");
                return;
            }

            // Get the variable name text (e.g. "message" or "command").
            var variableText = instanceSyntax.ToString();

            // First, check if an ancestor if-statement guards this invocation.
            if (IsInvocationGuardedByIf(instanceSyntax, variableText))
            {
                Console.WriteLine("[StringNullCheckAnalyzer] Guard clause found in an ancestor if-statement, skipping diagnostic.");
                return;
            }

            // Next, check if the containing method has an early guard clause for the variable.
            var methodDeclaration = instanceSyntax.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (methodDeclaration != null)
            {
                var semanticModel = context.Operation.SemanticModel;
                if (IsMethodGuardedForVariable(methodDeclaration, variableText, semanticModel))
                {
                    Console.WriteLine("[StringNullCheckAnalyzer] Guard clause found at method level, skipping diagnostic.");
                    return;
                }
            }

            Console.WriteLine("[StringNullCheckAnalyzer] No guard clause found, reporting diagnostic.");
            var diagnostic = Diagnostic.Create(Rule, instanceSyntax.GetLocation(), variableText);
            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Determines if the given expression is a literal or a constant.
        /// </summary>
        private static bool IsConstantOrLiteral(SyntaxNode expression, OperationAnalysisContext context)
        {
            var operation = context.Operation.SemanticModel.GetOperation(expression);
            return operation is ILiteralOperation ||
                   (operation is IFieldReferenceOperation fieldRef && fieldRef.Field.IsConst);
        }

        /// <summary>
        /// Checks if any ancestor if-statement contains a guard for the variable.
        /// This looks for conditions like "variable != null", "variable == null" (inverted with !), or
        /// calls to string.IsNullOrEmpty(variable) (or its negation).
        /// </summary>
        private static bool IsInvocationGuardedByIf(SyntaxNode instanceSyntax, string variableText)
        {
            foreach (var ancestor in instanceSyntax.Ancestors())
            {
                if (ancestor is IfStatementSyntax ifStatement)
                {
                    // Check if the if condition contains a null-check guard for the variable.
                    if (ContainsNullGuardCheck(ifStatement.Condition, variableText))
                    {
                        // If the invocation is located within the if-statement's block, consider it guarded.
                        if (ifStatement.Statement != null && ifStatement.Statement.Span.Contains(instanceSyntax.Span))
                        {
                            Console.WriteLine($"[StringNullCheckAnalyzer] Found guard in if-statement: {ifStatement.Condition}");
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the method declaration contains an early guard clause that checks the variable for null.
        /// For example, a statement like:
        ///     if (player == null || string.IsNullOrEmpty(message)) return null;
        /// is considered a guard clause for "message".
        /// </summary>
        private static bool IsMethodGuardedForVariable(MethodDeclarationSyntax methodDeclaration, string variableText, SemanticModel semanticModel)
        {
            if (methodDeclaration.Body == null)
                return false;

            // Only check the top-level statements in the method body.
            foreach (var statement in methodDeclaration.Body.Statements)
            {
                if (statement is IfStatementSyntax ifStatement)
                {
                    // Check if the if-statement's condition contains a null-check for the variable.
                    if (ContainsNullGuardCheck(ifStatement.Condition, variableText))
                    {
                        // Check if the if-statement's then branch contains a return statement.
                        if (ContainsReturnStatement(ifStatement.Statement))
                        {
                            Console.WriteLine($"[StringNullCheckAnalyzer] Found method-level guard: {ifStatement.Condition}");
                            return true;
                        }
                    }
                    // If the first statement is not an if-statement, assume no guard clause is present.
                    break;
                }
                else
                {
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Recursively checks if the condition expression contains a null-check for the given variable.
        /// Recognized patterns include:
        ///   - Binary expression: variable != null
        ///   - Invocation of string.IsNullOrEmpty(variable)
        ///   - Negated invocation: !string.IsNullOrEmpty(variable)
        ///   - Combined logical expressions (&&, ||)
        /// </summary>
        private static bool ContainsNullGuardCheck(ExpressionSyntax condition, string variableText)
        {
            if (condition == null)
                return false;

            // Check for binary expressions like "variable != null"
            if (condition is BinaryExpressionSyntax binary)
            {
                if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                {
                    if ((binary.Left.ToString() == variableText && binary.Right.IsKind(SyntaxKind.NullLiteralExpression)) ||
                        (binary.Right.ToString() == variableText && binary.Left.IsKind(SyntaxKind.NullLiteralExpression)))
                    {
                        return true;
                    }
                }

                // For logical AND/OR expressions, recursively check both sides.
                if (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression))
                {
                    return ContainsNullGuardCheck(binary.Left, variableText) || ContainsNullGuardCheck(binary.Right, variableText);
                }
            }

            // Check for invocation of string.IsNullOrEmpty(variable)
            if (condition is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    if (memberAccess.Name.Identifier.Text == "IsNullOrEmpty" && invocation.ArgumentList.Arguments.Count == 1)
                    {
                        var argText = invocation.ArgumentList.Arguments[0].Expression.ToString();
                        if (argText == variableText)
                        {
                            return true;
                        }
                    }
                }
            }

            // Check for negation of string.IsNullOrEmpty(variable), e.g. !string.IsNullOrEmpty(variable)
            if (condition is PrefixUnaryExpressionSyntax prefixUnary && prefixUnary.IsKind(SyntaxKind.LogicalNotExpression))
            {
                if (prefixUnary.Operand is InvocationExpressionSyntax innerInvocation)
                {
                    if (innerInvocation.Expression is MemberAccessExpressionSyntax innerMember &&
                        innerMember.Name.Identifier.Text == "IsNullOrEmpty" &&
                        innerInvocation.ArgumentList.Arguments.Count == 1)
                    {
                        var argText = innerInvocation.ArgumentList.Arguments[0].Expression.ToString();
                        if (argText == variableText)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the given statement (or any of its descendants) is a return statement.
        /// </summary>
        private static bool ContainsReturnStatement(StatementSyntax statement)
        {
            if (statement is ReturnStatementSyntax)
                return true;

            foreach (var descendant in statement.DescendantNodes())
            {
                if (descendant is ReturnStatementSyntax)
                    return true;
            }
            return false;
        }
    }
}
