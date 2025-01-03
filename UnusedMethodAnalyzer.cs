using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Collections.Generic;

namespace RustAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnusedMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RUST003";
        private const string Category = "Design";

        private static readonly LocalizableString Title = "Unused method detected";
        private static readonly LocalizableString MessageFormat = "Method '{0}' is never used";
        private static readonly LocalizableString MessageFormatWithHooks = "Method '{0}' is never used.\n" +
            "If you intended this to be a hook, no matching hook was found.\n" +
            "Similar hooks that might match: {1}";
        private static readonly LocalizableString MessageFormatCommand = "Method '{0}' is never used.\n" +
            "If you intended this to be a command, here are the common command signatures:\n" +
            "[Command(\"name\")]\n" +
            "void CommandName(IPlayer player, string command, string[] args)\n\n" +
            "[ChatCommand(\"name\")]\n" +
            "void CommandName(BasePlayer player, string command, string[] args)\n\n" +
            "[ConsoleCommand(\"name\")]\n" +
            "void CommandName(ConsoleSystem.Arg args)";
        private static readonly LocalizableString Description = "Methods should be used or removed to maintain clean code.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST003.md");

        private static readonly SymbolEqualityComparer SymbolComparer = SymbolEqualityComparer.Default;

        private static readonly Dictionary<SpecialType, string> SpecialTypeMap = new Dictionary<SpecialType, string>
        {
            { SpecialType.System_Boolean, "bool" },
            { SpecialType.System_Byte, "byte" },
            { SpecialType.System_SByte, "sbyte" },
            { SpecialType.System_Int16, "short" },
            { SpecialType.System_UInt16, "ushort" },
            { SpecialType.System_Int32, "int" },
            { SpecialType.System_UInt32, "uint" },
            { SpecialType.System_Int64, "long" },
            { SpecialType.System_UInt64, "ulong" },
            { SpecialType.System_Single, "float" },
            { SpecialType.System_Double, "double" },
            { SpecialType.System_Decimal, "decimal" },
            { SpecialType.System_Char, "char" },
            { SpecialType.System_String, "string" },
            { SpecialType.System_Object, "object" }
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeMethodInvocations, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethodInvocations(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
                return;

            // Skip special methods
            if (methodSymbol.MethodKind != MethodKind.Ordinary)
                return;

            // Skip methods with specific attributes
            var attributesToSkip = new[]
            {
                "ChatCommand",
                "Command",
                "ConsoleCommand",
                "HookMethod"
            };

            if (methodSymbol.GetAttributes().Any(attr =>
            {
                var attrName = attr.AttributeClass?.Name;
                return attrName != null && (
                    attributesToSkip.Contains(attrName) ||
                    attributesToSkip.Contains(attrName.Replace("Attribute", ""))
                );
            }))
            {
                return;
            }

            bool isUnityClass = IsUnityClass(methodSymbol.ContainingType);

            if (isUnityClass && UnityHooksConfiguration.IsHook(methodSymbol))
            {
                return;
            }

            if (HooksConfiguration.IsHook(methodSymbol)) 
            {
                return;
            }

            // Check if method is used
            if (!IsMethodUsed(methodSymbol, context))
            {
                bool diagnosticReported = false;

                // Check if method name contains "Command"
                if (methodSymbol.Name.ToLower().Contains("command"))
                {
                    var commandDiagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            DiagnosticId,
                            Title,
                            MessageFormatCommand,
                            Category,
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true,
                            description: Description,
                            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST003.md"),
                        methodSymbol.Locations[0],
                        methodSymbol.Name);

                    context.ReportDiagnostic(commandDiagnostic);
                    diagnosticReported = true;
                }

                // Get similar hook suggestions
                var allHooks = HooksConfiguration.HookSignatures
                    .Select(h => h.HookName)
                    .Distinct();
                    
                var similarHooks = StringDistance.FindSimilarShortNames(
                    methodSymbol.Name, 
                    allHooks, 
                    maxSuggestions: 3);
                    
                var suggestionsText = string.Join(", ", similarHooks);
                
                if (!string.IsNullOrEmpty(suggestionsText))
                {
                    var hookDiagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            DiagnosticId,
                            Title,
                            MessageFormatWithHooks,
                            Category,
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true,
                            description: Description,
                            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RUST003.md"),
                        methodSymbol.Locations[0],
                        methodSymbol.Name,
                        suggestionsText);

                    context.ReportDiagnostic(hookDiagnostic);
                    diagnosticReported = true;
                }

                // If no special cases were handled, show the basic message
                if (!diagnosticReported)
                {
                    var basicDiagnostic = Diagnostic.Create(
                        Rule,
                        methodSymbol.Locations[0],
                        methodSymbol.Name);

                    context.ReportDiagnostic(basicDiagnostic);
                }
            }
        }

        private static bool IsUnityClass(INamedTypeSymbol typeSymbol)
        {
            var current = typeSymbol;
            while (current != null)
            {
                if (current.ToDisplayString() == "UnityEngine.MonoBehaviour")
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
        }

        private static bool IsMethodUsed(IMethodSymbol method, SyntaxNodeAnalysisContext context)
        {
            // Ignore override methods
            if (method.IsOverride)
            {
                return true;
            }

            var root = context.Node.SyntaxTree.GetRoot(context.CancellationToken);
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol != null && SymbolComparer.Equals(symbolInfo.Symbol, method))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
