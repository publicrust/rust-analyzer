using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Configuration;

namespace RustAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RustVersionInitializer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor InitializationRule = new DiagnosticDescriptor(
            id: "RUST_INIT",
            title: "Rust Version Initialization",
            messageFormat: "Initializing Rust version: {0}",
            category: "Initialization",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor WrongVersionError = new DiagnosticDescriptor(
            id: "RUST002",
            title: "Invalid Rust Version",
            messageFormat: "Invalid Rust version '{0}' specified in project file. Only 'LastV2ersion1' is supported.",
            category: "Initialization",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "The RustVersion property in your project file must be set to 'LastV2ersion1'."
        );

        private static readonly DiagnosticDescriptor MissingVersionError = new DiagnosticDescriptor(
            id: "RUST001",
            title: "Missing Rust Version",
            messageFormat: "RustVersion property not found in project file. Add <RustVersion>LastV2ersion1</RustVersion> to your project file.",
            category: "Initialization",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(InitializationRule, WrongVersionError, MissingVersionError);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(
                syntaxContext =>
                {
                    var options = syntaxContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions;

                    if (!options.TryGetValue("build_property.rustversion", out var version))
                    {
                        syntaxContext.ReportDiagnostic(
                            Diagnostic.Create(MissingVersionError, Location.None)
                        );
                        return;
                    }

                    RustVersionProvider.Initialize(options);

                    if (RustVersionProvider.IsInitialized)
                    {
                        syntaxContext.ReportDiagnostic(
                            Diagnostic.Create(InitializationRule, Location.None, version)
                        );
                    }
                },
                SyntaxKind.CompilationUnit
            );
        }
    }
}
