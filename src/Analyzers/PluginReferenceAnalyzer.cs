using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RustAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PluginReferenceAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor InvalidMethodRule = new DiagnosticDescriptor(
            id: "RUST000040",
            title: "Invalid plugin method call",
            messageFormat: "Method '{0}' is not defined for plugin '{1}'. Available methods: {2}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "The called method is not defined in the plugin's configuration.",
            helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST000040.md"
        );

        private static readonly DiagnosticDescriptor InvalidParametersRule =
            new DiagnosticDescriptor(
                id: "RUST000041",
                title: "Invalid method parameters",
                messageFormat: "Method '{0}' of plugin '{1}' expects parameters: {2}",
                category: "Usage",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "The method call has invalid parameters.",
                helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST000041.md"
            );

        private static readonly DiagnosticDescriptor InvalidGenericTypeRule =
            new DiagnosticDescriptor(
                id: "RUST000043",
                title: "Invalid generic type for plugin method",
                messageFormat: "Method '{0}' returns '{1}' but trying to use as '{2}'",
                category: "Usage",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "The generic type parameter does not match the method's return type.",
                helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST000043.md"
            );

        private static readonly DiagnosticDescriptor VoidMethodWithGenericRule =
            new DiagnosticDescriptor(
                id: "RUST000044",
                title: "Void method with generic parameter",
                messageFormat: "Method '{0}' returns void and cannot be used with generic parameter",
                category: "Usage",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Void methods cannot be used with generic type parameters.",
                helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST000044.md"
            );

        private static readonly DiagnosticDescriptor DebugRule = new DiagnosticDescriptor(
            id: "RUST000042",
            title: "Debug Info",
            messageFormat: "{0}",
            category: "Debug",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Debug information from analyzer."
        );

        private SyntaxNodeAnalysisContext? _currentContext;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                InvalidMethodRule,
                InvalidParametersRule,
                InvalidGenericTypeRule,
                VoidMethodWithGenericRule,
                DebugRule
            );

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(
                AnalyzeInvocationExpression,
                SyntaxKind.InvocationExpression
            );
        }

        private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            _currentContext = context;
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Проверяем, что это вызов метода Call
            if (!IsCallMethod(invocation, out var pluginExpression))
            {
                return;
            }

            // Получаем имя плагина
            var pluginName = GetPluginName(pluginExpression);
            if (string.IsNullOrEmpty(pluginName))
            {
                ReportDebug(invocation, "Plugin name is empty");
                return;
            }

            // Проверяем, что поле имеет атрибут PluginReference
            var symbolInfo = context.SemanticModel.GetSymbolInfo(pluginExpression);
            var symbol = symbolInfo.Symbol;

            if (symbol == null)
            {
                ReportDebug(invocation, $"Symbol is null for {pluginName}");
                return;
            }

            if (!HasPluginReferenceAttribute(symbol))
            {
                ReportDebug(invocation, $"No PluginReference attribute for {pluginName}");
                return;
            }

            // Получаем имя вызываемого метода
            var arguments = invocation.ArgumentList.Arguments;
            if (arguments.Count == 0)
            {
                ReportDebug(invocation, "No arguments in Call method");
                return;
            }

            var firstArg = arguments[0];
            if (
                !(firstArg.Expression is LiteralExpressionSyntax literal)
                || literal.Kind() != SyntaxKind.StringLiteralExpression
            )
            {
                ReportDebug(invocation, "First argument is not a string literal");
                return;
            }

            var methodName = literal.Token.ValueText;

            // Проверяем, есть ли конфигурация для этого плагина
            if (!PluginMethodsConfiguration.HasPlugin(pluginName))
            {
                ReportDebug(invocation, $"No configuration for plugin {pluginName}");
                return;
            }

            var method = PluginMethodsConfiguration.GetMethod(pluginName, methodName);
            if (method == null)
            {
                // Метод не найден в конфигурации
                var config = PluginMethodsConfiguration.GetConfiguration(pluginName);
                if (config == null)
                {
                    ReportDebug(invocation, $"Configuration is null for {pluginName}");
                    return;
                }

                var availableMethods = string.Join(", ", config.Methods.Keys.OrderBy(m => m));

                var diagnostic = Diagnostic.Create(
                    InvalidMethodRule,
                    firstArg.GetLocation(),
                    methodName,
                    pluginName,
                    availableMethods
                );
                context.ReportDiagnostic(diagnostic);
                return;
            }

            // Проверяем generic тип
            var genericType = GetGenericType(invocation);

            if (genericType != null)
            {
                var returnType = method.ReturnType;

                if (returnType.Equals("void", StringComparison.OrdinalIgnoreCase))
                {
                    var diagnostic = Diagnostic.Create(
                        VoidMethodWithGenericRule,
                        invocation.GetLocation(),
                        methodName
                    );
                    context.ReportDiagnostic(diagnostic);
                    return;
                }

                if (!returnType.Equals(genericType.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    var diagnostic = Diagnostic.Create(
                        InvalidGenericTypeRule,
                        invocation.GetLocation(),
                        methodName,
                        returnType,
                        genericType
                    );
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }

            // Проверяем параметры метода
            var expectedParams = method
                .Parameters.Select(p =>
                    $"{p.Type} {p.Name}" + (p.IsOptional ? $" = {p.DefaultValue}" : "")
                )
                .ToList();

            if (arguments.Count > 1)
            {
                var actualParamCount = arguments.Count - 1; // Вычитаем имя метода
                var requiredParamCount = method.Parameters.Count(p => !p.IsOptional);

                if (
                    actualParamCount < requiredParamCount
                    || actualParamCount > method.Parameters.Count
                )
                {
                    var expectedParamsStr = string.Join(", ", expectedParams);
                    var diagnostic = Diagnostic.Create(
                        InvalidParametersRule,
                        invocation.ArgumentList.GetLocation(),
                        methodName,
                        pluginName,
                        expectedParamsStr
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
            else if (method.Parameters.Any(p => !p.IsOptional))
            {
                // Если метод требует параметры, но они не предоставлены
                var expectedParamsStr = string.Join(", ", expectedParams);
                var diagnostic = Diagnostic.Create(
                    InvalidParametersRule,
                    invocation.ArgumentList.GetLocation(),
                    methodName,
                    pluginName,
                    expectedParamsStr
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void ReportDebug(SyntaxNode node, string message)
        {
            if (_currentContext == null)
                return;
            var diagnostic = Diagnostic.Create(DebugRule, node.GetLocation(), message);
            _currentContext.Value.ReportDiagnostic(diagnostic);
        }

        private bool IsCallMethod(
            InvocationExpressionSyntax invocation,
            out ExpressionSyntax pluginExpression
        )
        {
            pluginExpression = null;

            // Проверяем прямой вызов метода (plugin.Call())
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name.Identifier.Text == "Call")
                {
                    pluginExpression = memberAccess.Expression;
                    return true;
                }
            }

            // Проверяем условный вызов метода (plugin?.Call())
            if (invocation.Expression is MemberBindingExpressionSyntax memberBinding)
            {
                if (memberBinding.Name.Identifier.Text == "Call")
                {
                    var parent = invocation.Parent;
                    while (parent != null && !(parent is ConditionalAccessExpressionSyntax))
                    {
                        parent = parent.Parent;
                    }

                    if (parent is ConditionalAccessExpressionSyntax conditionalAccess)
                    {
                        pluginExpression = conditionalAccess.Expression;
                        return true;
                    }
                }
            }

            return false;
        }

        private string GetPluginName(ExpressionSyntax expression)
        {
            if (expression is IdentifierNameSyntax identifier)
            {
                return identifier.Identifier.Text;
            }

            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Name.Identifier.Text;
            }

            return null;
        }

        private bool HasPluginReferenceAttribute(ISymbol symbol)
        {
            if (symbol is IFieldSymbol fieldSymbol)
            {
                var attributes = fieldSymbol.GetAttributes();
                foreach (var attr in attributes)
                {
                    var attrName = attr.AttributeClass?.Name;
                    if (attrName != null)
                    {
                        ReportDebug(
                            symbol
                                .Locations.First()
                                .SourceTree.GetRoot()
                                .FindNode(symbol.Locations.First().SourceSpan),
                            $"Found attribute: {attrName}"
                        );
                    }
                    if (attrName == "PluginReference" || attrName == "PluginReferenceAttribute")
                        return true;
                }
            }
            return false;
        }

        private TypeSyntax GetGenericType(InvocationExpressionSyntax invocation)
        {
            // Проверяем прямой вызов метода (plugin.Call<T>())
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name is GenericNameSyntax genericName)
                {
                    var type = genericName.TypeArgumentList.Arguments.First();
                    return type;
                }
            }

            // Проверяем условный вызов метода (plugin?.Call<T>())
            if (invocation.Expression is MemberBindingExpressionSyntax memberBinding)
            {
                if (memberBinding.Name is GenericNameSyntax genericName)
                {
                    var type = genericName.TypeArgumentList.Arguments.First();
                    return type;
                }
            }

            return null;
        }
    }
}
