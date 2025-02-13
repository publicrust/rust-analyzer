using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RustAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RestrictedNamespaceAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: "RUST000010",
            title: "Restricted namespace usage",
            messageFormat: "Usage of namespace '{0}' is restricted. Allowed types from this namespace: {1}\nYou can use these types in two ways:\n1. Full type name: {0}.{2}\n2. Using alias: using {2} = {0}.{2};",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "This namespace is in the list of restricted namespaces. Use only allowed types from this namespace.",
            helpLinkUri: "https://github.com/publicrust/rust-analyzer/blob/main/docs/RUST000010.md"
        );

        private readonly RestrictedNamespacesConfiguration _configuration;

        public RestrictedNamespaceAnalyzer()
        {
            _configuration = RestrictedNamespacesConfiguration.Create();
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);
            context.RegisterSyntaxNodeAction(AnalyzeQualifiedName, SyntaxKind.QualifiedName);
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierName, SyntaxKind.IdentifierName);
        }

        private void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
        {
            var usingDirective = (UsingDirectiveSyntax)context.Node;

            // Если это using с псевдонимом (alias)
            if (usingDirective.Alias != null)
            {
                var nameToCheck = usingDirective.Name?.ToString();
                if (!string.IsNullOrEmpty(nameToCheck))
                {
                    var symbolInfo = context.SemanticModel.GetSymbolInfo(usingDirective.Name);
                    var symbol = symbolInfo.Symbol;

                    // Проверяем, является ли правая часть типом или пространством имён
                    if (symbol is INamespaceSymbol)
                    {
                        var ns = nameToCheck;
                        if (IsRestrictedNamespace(ns))
                        {
                            var allowedTypes = GetAllowedTypesForNamespace(ns);
                            var firstType =
                                allowedTypes.Split(',').FirstOrDefault()?.Trim() ?? "Type";
                            var diagnostic = Diagnostic.Create(
                                Rule,
                                usingDirective.Name.GetLocation(),
                                ns,
                                allowedTypes,
                                firstType
                            );
                            context.ReportDiagnostic(diagnostic);
                        }
                        return;
                    }

                    // Если это тип, проверяем, разрешён ли он
                    var typeNamespace = GetNamespaceFromTypeName(nameToCheck);
                    if (IsRestrictedNamespace(typeNamespace) && !IsAllowedType(nameToCheck))
                    {
                        var allowedTypes = GetAllowedTypesForNamespace(typeNamespace);
                        var firstType = allowedTypes.Split(',').FirstOrDefault()?.Trim() ?? "Type";
                        var diagnostic = Diagnostic.Create(
                            Rule,
                            usingDirective.Name.GetLocation(),
                            typeNamespace,
                            allowedTypes,
                            firstType
                        );
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                return;
            }

            // Для обычного using проверяем пространство имён
            var namespaceName = usingDirective.Name?.ToString();
            if (string.IsNullOrEmpty(namespaceName))
                return;

            // Проверяем, является ли это пространство имён запрещённым
            if (IsRestrictedNamespace(namespaceName))
            {
                var allowedTypes = GetAllowedTypesForNamespace(namespaceName);
                var firstType = allowedTypes.Split(',').FirstOrDefault()?.Trim() ?? "Type";
                var diagnostic = Diagnostic.Create(
                    Rule,
                    usingDirective.Name.GetLocation(),
                    namespaceName,
                    allowedTypes,
                    firstType
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeQualifiedName(SyntaxNodeAnalysisContext context)
        {
            var qualifiedName = (QualifiedNameSyntax)context.Node;
            var fullName = qualifiedName.ToString();

            if (string.IsNullOrEmpty(fullName))
                return;

            // Если это разрешённый тип, пропускаем
            if (IsAllowedType(fullName))
                return;

            var ns = GetNamespaceFromTypeName(fullName);
            if (IsRestrictedNamespace(ns))
            {
                var allowedTypes = GetAllowedTypesForNamespace(ns);
                var firstType = allowedTypes.Split(',').FirstOrDefault()?.Trim() ?? "Type";
                var diagnostic = Diagnostic.Create(
                    Rule,
                    qualifiedName.GetLocation(),
                    ns,
                    allowedTypes,
                    firstType
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeIdentifierName(SyntaxNodeAnalysisContext context)
        {
            var identifier = (IdentifierNameSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier);

            if (symbolInfo.Symbol?.ContainingNamespace == null)
                return;

            var containingNamespace = symbolInfo.Symbol.ContainingNamespace.ToDisplayString();
            var fullTypeName = $"{containingNamespace}.{identifier}";

            // Если это разрешённый тип, пропускаем
            if (IsAllowedType(fullTypeName))
                return;

            if (IsRestrictedNamespace(containingNamespace))
            {
                var allowedTypes = GetAllowedTypesForNamespace(containingNamespace);
                var firstType = allowedTypes.Split(',').FirstOrDefault()?.Trim() ?? "Type";
                var diagnostic = Diagnostic.Create(
                    Rule,
                    identifier.GetLocation(),
                    containingNamespace,
                    allowedTypes,
                    firstType
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsRestrictedNamespace(string namespaceName)
        {
            if (string.IsNullOrEmpty(namespaceName))
                return false;

            return _configuration.RestrictedNamespaces.Any(restricted =>
                namespaceName.Equals(restricted, StringComparison.Ordinal)
                || namespaceName.StartsWith(restricted + ".", StringComparison.Ordinal)
            );
        }

        private bool IsAllowedType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;

            // Проверяем точное совпадение с разрешённым типом
            return _configuration.AllowedTypes.Contains(typeName);
        }

        private string GetNamespaceFromTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return string.Empty;

            var lastDotIndex = typeName.LastIndexOf('.');
            return lastDotIndex > 0 ? typeName.Substring(0, lastDotIndex) : typeName;
        }

        private string GetAllowedTypesForNamespace(string namespaceName)
        {
            if (string.IsNullOrEmpty(namespaceName))
                return string.Empty;

            // Получаем только типы из конкретного пространства имён (без вложенных)
            var allowedTypes = _configuration
                .AllowedTypes.Where(t =>
                {
                    var typeNamespace = GetNamespaceFromTypeName(t);
                    return typeNamespace.Equals(namespaceName, StringComparison.Ordinal);
                })
                .Select(t => t.Substring(t.LastIndexOf('.') + 1))
                .OrderBy(t => t);

            return string.Join(", ", allowedTypes);
        }
    }
}
