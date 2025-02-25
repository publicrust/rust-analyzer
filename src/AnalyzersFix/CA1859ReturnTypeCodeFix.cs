using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace RustAnalyzer.AnalyzersFix
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CA1859ReturnTypeCodeFix)), Shared]
    public class CA1859ReturnTypeCodeFix : CodeFixProvider
    {
        private const string DiagnosticId = "CA1859"; // ID для анализатора Microsoft CA1859
        
        public sealed override ImmutableArray<string> FixableDiagnosticIds => 
            ImmutableArray.Create(DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => 
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            
            // Получаем диагностическое сообщение
            var diagnostic = context.Diagnostics.First(d => d.Id == DiagnosticId);
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Находим метод, который нужно исправить
            var methodDeclaration = root.FindToken(diagnosticSpan.Start)
                .Parent?
                .AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (methodDeclaration == null)
                return;

            // Проверяем, что метод возвращает object
            if (!IsObjectReturnType(methodDeclaration))
                return;

            // Определяем, какой тип фактически возвращает метод
            var typeInfo = DetermineActualReturnType(methodDeclaration, semanticModel);
            
            if (typeInfo.ActualType != null)
            {
                // Формируем имя типа с учетом nullable
                string typeName = typeInfo.ActualType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                string displayName = typeName;
                
                // Добавляем ? для value типов, если нужно nullable
                if (typeInfo.ShouldBeNullable && typeInfo.ActualType.IsValueType && 
                    typeInfo.ActualType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
                {
                    displayName = $"{typeName}?";
                }
                // Добавляем ? для nullable reference типов, если проект поддерживает nullable references
                else if (typeInfo.ShouldBeNullable && !typeInfo.ActualType.IsValueType)
                {
                    var nullableContext = semanticModel.GetNullableContext(methodDeclaration.SpanStart);
                    bool nullableEnabled = (nullableContext & NullableContext.Enabled) == NullableContext.Enabled;
                    
                    if (nullableEnabled)
                    {
                        displayName = $"{typeName}?";
                    }
                }
                
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Change return type to '{displayName}'",
                        createChangedDocument: c => ChangeReturnTypeAsync(context.Document, methodDeclaration, typeInfo, c),
                        equivalenceKey: $"ChangeReturnTypeTo{displayName}"),
                    diagnostic);
            }
        }

        private bool IsObjectReturnType(MethodDeclarationSyntax methodDeclaration)
        {
            // Проверяем, что возвращаемый тип - object
            if (methodDeclaration.ReturnType is PredefinedTypeSyntax predefinedType &&
                predefinedType.Keyword.ValueText == "object")
            {
                return true;
            }
            
            // Проверяем, что возвращаемый тип - System.Object
            if (methodDeclaration.ReturnType is IdentifierNameSyntax identifierName &&
                identifierName.Identifier.ValueText == "Object")
            {
                return true;
            }

            // Проверяем полное имя - System.Object
            if (methodDeclaration.ReturnType is QualifiedNameSyntax qualifiedName &&
                qualifiedName.ToString() == "System.Object")
            {
                return true;
            }

            return false;
        }

        private class TypeAnalysisResult
        {
            public ITypeSymbol ActualType { get; set; }
            public bool ShouldBeNullable { get; set; }
        }

        private TypeAnalysisResult DetermineActualReturnType(MethodDeclarationSyntax methodDecl, SemanticModel semanticModel)
        {
            // Если метода нет тела, не можем анализировать
            if (methodDecl.Body == null)
                return new TypeAnalysisResult();

            // Ищем все return-выражения в методе
            var returnStatements = methodDecl.Body.DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .Where(rs => rs.Expression != null)
                .ToList();

            if (returnStatements.Count == 0)
                return new TypeAnalysisResult();

            // Отслеживаем явные типы возвратов
            bool hasNullReturns = false;
            bool hasBoolReturns = false;
            bool hasOtherReturns = false;
            ITypeSymbol otherType = null;

            // Отслеживаем специфические литералы
            bool hasTrueLiteral = false;
            bool hasFalseLiteral = false;
            
            // Словарь для подсчета типов
            var typeFrequency = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
            
            // Анализируем все return-выражения
            foreach (var returnStatement in returnStatements)
            {
                var returnExpr = returnStatement.Expression;
                
                // Проверяем возврат литерала
                if (returnExpr is LiteralExpressionSyntax literal)
                {
                    // Проверяем на null
                    if (literal.Kind() == SyntaxKind.NullLiteralExpression)
                    {
                        hasNullReturns = true;
                        continue;
                    }
                    // Проверяем на true/false
                    else if (literal.Kind() == SyntaxKind.TrueLiteralExpression)
                    {
                        hasTrueLiteral = true;
                        hasBoolReturns = true;
                        var boolType = semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);
                        IncrementTypeFrequency(typeFrequency, boolType);
                        continue;
                    }
                    else if (literal.Kind() == SyntaxKind.FalseLiteralExpression)
                    {
                        hasFalseLiteral = true;
                        hasBoolReturns = true;
                        var boolType = semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);
                        IncrementTypeFrequency(typeFrequency, boolType);
                        continue;
                    }
                }

                // Проверяем условные выражения, часто используемые для возврата логических значений
                if (returnExpr is BinaryExpressionSyntax binaryExpr)
                {
                    if (binaryExpr.Kind() == SyntaxKind.EqualsExpression || 
                        binaryExpr.Kind() == SyntaxKind.NotEqualsExpression ||
                        binaryExpr.Kind() == SyntaxKind.GreaterThanExpression ||
                        binaryExpr.Kind() == SyntaxKind.LessThanExpression ||
                        binaryExpr.Kind() == SyntaxKind.GreaterThanOrEqualExpression ||
                        binaryExpr.Kind() == SyntaxKind.LessThanOrEqualExpression)
                    {
                        hasBoolReturns = true;
                        var boolType = semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);
                        IncrementTypeFrequency(typeFrequency, boolType);
                        continue;
                    }
                }
                
                // Проверяем тернарный оператор с bool или null значениями
                if (returnExpr is ConditionalExpressionSyntax conditionalExpr)
                {
                    bool containsOnlyBoolOrNull = true;
                    
                    // Проверяем true ветку
                    if (conditionalExpr.WhenTrue is LiteralExpressionSyntax trueExpr)
                    {
                        if (trueExpr.Kind() == SyntaxKind.TrueLiteralExpression)
                            hasTrueLiteral = true;
                        else if (trueExpr.Kind() == SyntaxKind.FalseLiteralExpression)
                            hasFalseLiteral = true;
                        else if (trueExpr.Kind() == SyntaxKind.NullLiteralExpression)
                            hasNullReturns = true;
                        else
                            containsOnlyBoolOrNull = false;
                    }
                    else
                    {
                        containsOnlyBoolOrNull = false;
                    }
                    
                    // Проверяем false ветку
                    if (conditionalExpr.WhenFalse is LiteralExpressionSyntax falseExpr)
                    {
                        if (falseExpr.Kind() == SyntaxKind.TrueLiteralExpression)
                            hasTrueLiteral = true;
                        else if (falseExpr.Kind() == SyntaxKind.FalseLiteralExpression)
                            hasFalseLiteral = true; 
                        else if (falseExpr.Kind() == SyntaxKind.NullLiteralExpression)
                            hasNullReturns = true;
                        else
                            containsOnlyBoolOrNull = false;
                    }
                    else
                    {
                        containsOnlyBoolOrNull = false;
                    }
                    
                    // Если тернарный оператор содержит только bool и null значения, считаем это bool
                    if (containsOnlyBoolOrNull && (hasTrueLiteral || hasFalseLiteral))
                    {
                        hasBoolReturns = true;
                        var boolType = semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);
                        IncrementTypeFrequency(typeFrequency, boolType);
                        continue;
                    }
                }
                
                // Получаем тип выражения для остальных случаев
                var typeInfo = semanticModel.GetTypeInfo(returnExpr);
                if (typeInfo.Type != null && typeInfo.Type.SpecialType != SpecialType.System_Object)
                {
                    // Если тип boolean, отмечаем это
                    if (typeInfo.Type.SpecialType == SpecialType.System_Boolean)
                    {
                        hasBoolReturns = true;
                    }
                    else
                    {
                        hasOtherReturns = true;
                        otherType = typeInfo.Type;
                    }
                    
                    IncrementTypeFrequency(typeFrequency, typeInfo.Type);
                }
            }

            // Особая обработка методов, которые возвращают только логические значения и null
            if ((hasTrueLiteral || hasFalseLiteral) && hasNullReturns && !hasOtherReturns)
            {
                var boolType = semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);
                
                // Создаем nullable bool (если проект поддерживает nullable)
                var compilation = semanticModel.Compilation;
                var nullableType = compilation.GetTypeByMetadataName("System.Nullable`1");
                if (nullableType != null)
                {
                    return new TypeAnalysisResult
                    {
                        ActualType = nullableType.Construct(boolType),
                        ShouldBeNullable = true
                    };
                }
                
                // Если не удалось создать Nullable<bool>, возвращаем bool с флагом на nullable
                return new TypeAnalysisResult
                {
                    ActualType = boolType,
                    ShouldBeNullable = true
                };
            }

            // Если нет типов кроме object, используем bool если есть true/false литералы
            if (typeFrequency.Count == 0)
            {
                bool hasBooleanLiterals = hasTrueLiteral || hasFalseLiteral;
                    
                if (hasBooleanLiterals)
                {
                    return new TypeAnalysisResult
                    {
                        ActualType = semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean),
                        // bool - value type с null должен быть nullable
                        ShouldBeNullable = hasNullReturns
                    };
                }
                
                return new TypeAnalysisResult();
            }
            
            // Возвращаем тип с наибольшей частотой
            var mostFrequentType = typeFrequency.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
            
            // Определяем, должен ли тип быть nullable
            bool shouldBeNullable = hasNullReturns;
            
            // Если у нас value type и возврат null, нужно использовать Nullable<T>
            if (hasNullReturns && mostFrequentType.IsValueType)
            {
                var compilation = semanticModel.Compilation;
                var nullableType = compilation.GetTypeByMetadataName("System.Nullable`1");
                if (nullableType != null)
                {
                    mostFrequentType = nullableType.Construct(mostFrequentType);
                }
            }
            
            return new TypeAnalysisResult
            {
                ActualType = mostFrequentType,
                ShouldBeNullable = shouldBeNullable
            };
        }

        private void IncrementTypeFrequency(Dictionary<ITypeSymbol, int> typeFrequency, ITypeSymbol type)
        {
            if (typeFrequency.ContainsKey(type))
            {
                typeFrequency[type]++;
            }
            else
            {
                typeFrequency[type] = 1;
            }
        }

        private async Task<Document> ChangeReturnTypeAsync(
            Document document, 
            MethodDeclarationSyntax methodDecl, 
            TypeAnalysisResult typeInfo, 
            CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var editor = new SyntaxEditor(root, document.Project.Solution.Workspace);
            var generator = SyntaxGenerator.GetGenerator(document);

            // Создаем новый тип возвращаемого значения
            TypeSyntax newReturnType;
            bool isNullableContext = (semanticModel.GetNullableContext(methodDecl.SpanStart) & NullableContext.Enabled) == NullableContext.Enabled;
            
            // Обработка специальных типов
            if (typeInfo.ActualType.SpecialType == SpecialType.System_Boolean)
            {
                newReturnType = SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(SyntaxKind.BoolKeyword));
                
                // Если тип bool и должен быть nullable, добавляем ?
                if (typeInfo.ShouldBeNullable)
                {
                    newReturnType = SyntaxFactory.NullableType(newReturnType);
                }
            }
            else if (typeInfo.ActualType.SpecialType == SpecialType.System_Int32)
            {
                newReturnType = SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(SyntaxKind.IntKeyword));
                
                // Если тип int и должен быть nullable, добавляем ?
                if (typeInfo.ShouldBeNullable)
                {
                    newReturnType = SyntaxFactory.NullableType(newReturnType);
                }
            }
            else if (typeInfo.ActualType.SpecialType == SpecialType.System_String)
            {
                newReturnType = SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(SyntaxKind.StringKeyword));
                
                // Добавляем ? для nullable если нужно и включены nullable reference types
                if (typeInfo.ShouldBeNullable && isNullableContext)
                {
                    newReturnType = SyntaxFactory.NullableType(newReturnType);
                }
            }
            else if (typeInfo.ActualType.SpecialType == SpecialType.System_Double)
            {
                newReturnType = SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(SyntaxKind.DoubleKeyword));
                
                // Если тип double и должен быть nullable, добавляем ?
                if (typeInfo.ShouldBeNullable)
                {
                    newReturnType = SyntaxFactory.NullableType(newReturnType);
                }
            }
            else
            {
                // Для nullable value типов (Nullable<T>)
                if (typeInfo.ActualType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    var underlyingType = ((INamedTypeSymbol)typeInfo.ActualType).TypeArguments[0];
                    
                    if (underlyingType.SpecialType == SpecialType.System_Int32)
                    {
                        newReturnType = SyntaxFactory.NullableType(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)));
                    }
                    else if (underlyingType.SpecialType == SpecialType.System_Boolean)
                    {
                        newReturnType = SyntaxFactory.NullableType(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)));
                    }
                    else if (underlyingType.SpecialType == SpecialType.System_Double)
                    {
                        newReturnType = SyntaxFactory.NullableType(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.DoubleKeyword)));
                    }
                    else
                    {
                        var innerType = (TypeSyntax)generator.TypeExpression(underlyingType);
                        newReturnType = SyntaxFactory.NullableType(innerType);
                    }
                }
                else
                {
                    // Для других типов используем полное имя
                    newReturnType = (TypeSyntax)generator.TypeExpression(typeInfo.ActualType);
                    
                    // Добавляем ? для nullable если нужно и включены nullable reference types
                    if (typeInfo.ShouldBeNullable && !typeInfo.ActualType.IsValueType && isNullableContext)
                    {
                        newReturnType = SyntaxFactory.NullableType(newReturnType);
                    }
                }
            }

            // Заменяем тип возвращаемого значения
            var newMethodDecl = methodDecl.WithReturnType(newReturnType);
            
            // Проверяем, нужно ли изменить также реализацию метода
            if (methodDecl.Body != null)
            {
                // Ищем все return-выражения в методе
                var returnStatements = methodDecl.Body.DescendantNodes()
                    .OfType<ReturnStatementSyntax>()
                    .Where(rs => rs.Expression != null)
                    .ToList();

                foreach (var returnStatement in returnStatements)
                {
                    if (returnStatement.Expression is LiteralExpressionSyntax literal)
                    {
                        // Заменяем null на default для типа
                        if (literal.Kind() == SyntaxKind.NullLiteralExpression)
                        {
                            ExpressionSyntax defaultExpr;
                            
                            // Для value типов без Nullable<T> заменяем null на default
                            if (typeInfo.ActualType.IsValueType && 
                                typeInfo.ActualType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
                            {
                                // Создаем подходящее значение по умолчанию для типа
                                if (typeInfo.ActualType.SpecialType == SpecialType.System_Boolean)
                                {
                                    defaultExpr = SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);
                                }
                                else
                                {
                                    // Для других value типов используем default
                                    defaultExpr = SyntaxFactory.DefaultExpression(newReturnType);
                                }
                                
                                var newReturnStatement = returnStatement.WithExpression(defaultExpr);
                                editor.ReplaceNode(returnStatement, newReturnStatement);
                            }
                            // Для Nullable<T> и reference типов оставляем null как есть
                        }
                    }
                    else
                    {
                        // Получаем тип текущего выражения
                        var exprType = semanticModel.GetTypeInfo(returnStatement.Expression).Type;
                        
                        // Если тип не совпадает с целевым, добавляем явное приведение
                        if (exprType != null && !SymbolEqualityComparer.Default.Equals(exprType, typeInfo.ActualType) &&
                            exprType.SpecialType != SpecialType.System_Object)
                        {
                            // Для nullable типов мы должны сравнивать с базовым типом
                            bool typesCompatible = SymbolEqualityComparer.Default.Equals(exprType, typeInfo.ActualType);
                            
                            if (!typesCompatible && typeInfo.ActualType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                            {
                                var underlyingType = ((INamedTypeSymbol)typeInfo.ActualType).TypeArguments[0];
                                typesCompatible = SymbolEqualityComparer.Default.Equals(exprType, underlyingType);
                            }
                            
                            if (!typesCompatible)
                            {
                                // Пытаемся определить совместимость типов
                                var conversion = semanticModel.ClassifyConversion(
                                    returnStatement.Expression.Kind() == SyntaxKind.NullLiteralExpression 
                                        ? SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)) 
                                        : returnStatement.Expression, 
                                    typeInfo.ActualType);

                                // Создаем выражение с приведением типа
                                ExpressionSyntax convertedExpr;
                                if (conversion.Exists && !conversion.IsExplicit)
                                {
                                    // Неявное преобразование - оставляем как есть
                                    continue;
                                }
                                else
                                {
                                    // Явное преобразование
                                    convertedExpr = SyntaxFactory.CastExpression(
                                        newReturnType,
                                        returnStatement.Expression);
                                }
                                
                                var newReturnStatement = returnStatement.WithExpression(convertedExpr);
                                editor.ReplaceNode(returnStatement, newReturnStatement);
                            }
                        }
                    }
                }
            }

            // Заменяем объявление метода
            editor.ReplaceNode(methodDecl, newMethodDecl);

            // Возвращаем обновленный документ
            return document.WithSyntaxRoot(editor.GetChangedRoot());
        }
    }
} 