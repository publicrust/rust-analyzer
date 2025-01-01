using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace RustAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ReflectionAccessAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST0006";
        private const string Category = "Usage";
        private const string LogPrefix = "[ReflectionAnalyzer] ";

        // Single-line message without trailing period, as per RS1032
        private const string MessageFormat = "Cannot access {1} {2} field '{3}' in type '{0}' - Use reflection with: var field = typeof({0}).GetField(\"{3}\", System.Reflection.BindingFlags.{1} | System.Reflection.BindingFlags.Instance); {2} value = ({2})field.GetValue(instance)";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Inaccessible field access",
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Field is not accessible due to its protection level. Use reflection to access it if needed");

        [System.Runtime.CompilerServices.CompilerGenerated]
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        private void Log(string message, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            Debug.WriteLine($"{LogPrefix}[{memberName}] {message}");
        }

        public override void Initialize(AnalysisContext context)
        {
            Log("Initializing analyzer");
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            
            context.RegisterOperationAction(
                AnalyzeOperation, 
                OperationKind.FieldReference,
                OperationKind.PropertyReference);
                
            context.RegisterSyntaxNodeAction(
                AnalyzeNode,
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxKind.IdentifierName);
        }

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            var operation = context.Operation;
            if (operation == null)
            {
                Log("Operation is null");
                return;
            }

            Log($"Analyzing operation: Kind={operation.Kind}, Type={operation.Type?.Name ?? "unknown"}");

            if (operation is IFieldReferenceOperation fieldRef)
            {
                if (fieldRef.Field == null)
                {
                    Log("Field reference is null");
                    return;
                }

                Log($"Found field reference: {fieldRef.Field.Name} in {fieldRef.Field.ContainingType?.Name ?? "unknown"}");
                
                var location = operation.Syntax.GetLocation();
                if (location != null)
                {
                    CheckFieldAccess(fieldRef.Field, context.ContainingSymbol, location, context);
                }
            }
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            if (node == null)
            {
                Log("Node is null");
                return;
            }

            Log($"Analyzing node: Kind={node.Kind()}, Text={node.GetText().ToString().Trim()}");

            switch (node)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    AnalyzeMemberAccess(context, memberAccess);
                    break;
                case IdentifierNameSyntax identifier:
                    AnalyzeIdentifier(context, identifier);
                    break;
            }
        }

        private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, MemberAccessExpressionSyntax memberAccess)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Name);
            Log($"Member access: Expression={memberAccess.Expression}, Name={memberAccess.Name}, SymbolKind={symbolInfo.Symbol?.Kind}");

            if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
            {
                Log($"Found field symbol: {fieldSymbol.Name} in {fieldSymbol.ContainingType?.Name ?? "unknown"}");
                CheckFieldAccess(fieldSymbol, context.ContainingSymbol, memberAccess.Name.GetLocation(), context);
            }
        }

        private void AnalyzeIdentifier(SyntaxNodeAnalysisContext context, IdentifierNameSyntax identifier)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier);
            Log($"Identifier: Name={identifier.Identifier.Text}, SymbolKind={symbolInfo.Symbol?.Kind}");

            if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
            {
                Log($"Found field symbol: {fieldSymbol.Name} in {fieldSymbol.ContainingType?.Name ?? "unknown"}");
                CheckFieldAccess(fieldSymbol, context.ContainingSymbol, identifier.GetLocation(), context);
            }
        }

        private void CheckFieldAccess(IFieldSymbol fieldSymbol, ISymbol? containingSymbol, Location location, SyntaxNodeAnalysisContext context)
        {
            if (fieldSymbol == null || location == null)
            {
                Log("Field symbol or location is null");
                return;
            }

            var currentType = containingSymbol?.ContainingType;
            Log($"Checking access: Field={fieldSymbol.Name}, CurrentType={currentType?.Name ?? "unknown"}");

            if (!HasAccess(fieldSymbol, currentType))
            {
                var accessibilityFlag = GetAccessibilityFlag(fieldSymbol.DeclaredAccessibility);
                var accessibilityName = GetAccessibilityName(fieldSymbol.DeclaredAccessibility);
                var fieldType = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var containingTypeName = fieldSymbol.ContainingType?.Name ?? "Unknown";

                Log($"Access denied: Field={fieldSymbol.Name}, Type={containingTypeName}, " +
                    $"Accessibility={accessibilityName}, CurrentType={currentType?.Name ?? "unknown"}");

                var diagnostic = Diagnostic.Create(Rule,
                    location,
                    containingTypeName,
                    accessibilityFlag,
                    fieldType,
                    fieldSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
            else
            {
                Log($"Access granted: Field={fieldSymbol.Name}");
            }
        }

        private void CheckFieldAccess(IFieldSymbol fieldSymbol, ISymbol? containingSymbol, Location location, OperationAnalysisContext context)
        {
            if (fieldSymbol == null || location == null)
            {
                Log("Field symbol or location is null");
                return;
            }

            var currentType = containingSymbol?.ContainingType;
            Log($"Checking access: Field={fieldSymbol.Name}, CurrentType={currentType?.Name ?? "unknown"}");

            if (!HasAccess(fieldSymbol, currentType))
            {
                var accessibilityFlag = GetAccessibilityFlag(fieldSymbol.DeclaredAccessibility);
                var accessibilityName = GetAccessibilityName(fieldSymbol.DeclaredAccessibility);
                var fieldType = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var containingTypeName = fieldSymbol.ContainingType?.Name ?? "Unknown";

                Log($"Access denied: Field={fieldSymbol.Name}, Type={containingTypeName}, " +
                    $"Accessibility={accessibilityName}, CurrentType={currentType?.Name ?? "unknown"}");

                var diagnostic = Diagnostic.Create(Rule,
                    location,
                    containingTypeName,
                    accessibilityFlag,
                    fieldType,
                    fieldSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
            else
            {
                Log($"Access granted: Field={fieldSymbol.Name}");
            }
        }

        private bool HasAccess(IFieldSymbol field, INamedTypeSymbol? currentType)
        {
            if (field == null)
            {
                Log("Field is null");
                return false;
            }

            var accessibility = field.DeclaredAccessibility;
            var containingType = field.ContainingType;
            var currentAssembly = currentType?.ContainingAssembly;
            var sameType = currentType != null && SymbolEqualityComparer.Default.Equals(currentType, containingType);
            var sameAssembly = currentAssembly != null && field.ContainingAssembly != null && 
                              SymbolEqualityComparer.Default.Equals(field.ContainingAssembly, currentAssembly);
            
            Log($"Access check: Field={field.Name}, " +
                $"Accessibility={accessibility}, " +
                $"ContainingType={containingType?.Name ?? "unknown"}, " +
                $"CurrentType={currentType?.Name ?? "unknown"}, " +
                $"SameType={sameType}, " +
                $"SameAssembly={sameAssembly}");

            if (accessibility == Accessibility.Public)
            {
                Log($"Field {field.Name} is public - access granted");
                return true;
            }

            if (currentType == null)
            {
                Log($"No current type - access denied");
                return false;
            }

            if (sameType)
            {
                Log($"Same type - access granted");
                return true;
            }

            if (accessibility == Accessibility.Protected)
            {
                var baseTypes = GetBaseTypes(currentType).ToList();
                var hasAccess = baseTypes.Any(t => t != null && SymbolEqualityComparer.Default.Equals(t, containingType));
                Log($"Protected field check: HasBaseTypeAccess={hasAccess}, BaseTypes=[{string.Join(", ", baseTypes.Select(t => t?.Name ?? "unknown"))}]");
                return hasAccess;
            }

            if (accessibility == Accessibility.Internal && sameAssembly)
            {
                Log($"Internal field in same assembly - access granted");
                return true;
            }

            Log($"No access rules matched - access denied");
            return false;
        }

        private static IEnumerable<INamedTypeSymbol> GetBaseTypes(INamedTypeSymbol type)
        {
            var current = type.BaseType;
            while (current != null)
            {
                yield return current;
                current = current.BaseType;
            }
        }

        private static string GetAccessibilityFlag(Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.Private => "NonPublic",
                Accessibility.Protected => "NonPublic",
                Accessibility.Internal => "NonPublic",
                _ => "Public"
            };
        }

        private static string GetAccessibilityName(Accessibility accessibility)
        {
            return accessibility.ToString().ToLower();
        }
    }
}
