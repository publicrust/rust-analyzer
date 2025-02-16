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
    public class PluginReferenceNullableAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RA0040";
        private const string Category = "Usage";

        private static readonly LocalizableString Title =
            "Plugin reference field should be nullable";

        private static readonly string NoteTemplate =
            "field '{0}' of type '{1}' should be declared as nullable";

        private static readonly string HelpTemplate =
            "change the type to '{2}' to indicate that the plugin may not be available at runtime";

        private static readonly string ExampleTemplate = "private readonly {2} {0};";

        private static readonly LocalizableString Description =
            "Fields marked with [PluginReference] attribute should be declared as nullable types to indicate that they may not be available at runtime.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            "{0}", // Placeholder for dynamic description
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/legov/rust-analyzer/blob/main/docs/RA0040.md"
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        }

        private void AnalyzeField(SyntaxNodeAnalysisContext context)
        {
            var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

            // Проверяем наличие атрибута [PluginReference]
            if (!HasPluginReferenceAttribute(fieldDeclaration))
                return;

            // Проверяем каждую переменную в объявлении поля
            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                // Получаем тип поля
                var typeInfo = context.SemanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type);
                var fieldType = typeInfo.Type;
                if (fieldType == null)
                    continue;

                // Проверяем, является ли тип nullable
                bool isNullable =
                    fieldDeclaration.Declaration.Type is NullableTypeSyntax
                    || (fieldType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

                if (!isNullable)
                {
                    var location = variable.GetLocation();
                    var sourceText = location.SourceTree?.GetText();
                    if (sourceText == null)
                        continue;

                    var parameters = new[]
                    {
                        variable.Identifier.Text, // {0} - имя поля
                        fieldType.ToDisplayString(), // {1} - текущий тип
                        fieldType.ToDisplayString() + "?", // {2} - правильный тип (с ?)
                    };

                    var formatInfo = new RustDiagnosticFormatter.DiagnosticFormatInfo
                    {
                        ErrorCode = DiagnosticId,
                        ErrorTitle = "field with [PluginReference] attribute must be nullable",
                        Location = location,
                        SourceText = sourceText,
                        MessageParameters = parameters,
                        Note = NoteTemplate,
                        Help = HelpTemplate,
                        Example = ExampleTemplate,
                    };

                    var dynamicDescription = RustDiagnosticFormatter.FormatDiagnostic(formatInfo);
                    var diagnostic = Diagnostic.Create(Rule, location, dynamicDescription);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private bool HasPluginReferenceAttribute(FieldDeclarationSyntax fieldDeclaration)
        {
            return fieldDeclaration
                .AttributeLists.SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() == "PluginReference");
        }
    }
}
