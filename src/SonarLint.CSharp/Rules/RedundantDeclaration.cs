/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("1min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    [Tags(Tag.Clumsy, Tag.Finding)]
    public class RedundantDeclaration : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3257";
        internal const string Title = "Declarations and initializations should be as concise as possible";
        internal const string Description =
            "Unnecessarily verbose declarations and initializations make it harder to read the code, and should be simplified.";
        internal const string MessageFormat = "Remove the {0}; it is redundant.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), true,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        internal const string DiagnosticTypeKey = "diagnosticType";

        internal enum RedundancyType
        {
            LambdaParameterType,
            ArraySize,
            ArrayType,
            ExplicitDelegate,
            ExplicitNullable,
            ObjectInitializer,
            DelegateParameterList
        }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    ReportOnExplicitDelegateCreation(c);
                    ReportRedundantNullableConstructorCall(c);
                    ReportOnRedundantObjectInitializer(c);
                },
                SyntaxKind.ObjectCreationExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ReportOnRedundantParameterList(c),
                SyntaxKind.AnonymousMethodExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ReportRedundancyInArrayCreation(c),
                SyntaxKind.ArrayCreationExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ReportRedundantTypeSpecificationInLambda(c),
                SyntaxKind.ParenthesizedLambdaExpression);
        }

        private static void ReportRedundantTypeSpecificationInLambda(SyntaxNodeAnalysisContext c)
        {
            var lambda = (ParenthesizedLambdaExpressionSyntax)c.Node;
            if (lambda.ParameterList == null ||
                lambda.ParameterList.Parameters.Any(
                    p => p.Type == null ||
                    p.Modifiers.Any(m =>
                        m.IsKind(SyntaxKind.RefKeyword) ||
                        m.IsKind(SyntaxKind.OutKeyword))))
            {
                return;
            }

            var symbol = c.SemanticModel.GetSymbolInfo(lambda).Symbol as IMethodSymbol;
            if(symbol == null)
            {
                return;
            }

            var newParameterList = SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(lambda.ParameterList.Parameters.Select(p => SyntaxFactory.Parameter(p.Identifier))));
            var newLambda = lambda.WithParameterList(newParameterList);

            SemanticModel newSemanticModel;
            newLambda = ChangeSyntaxElement(lambda, newLambda, c.SemanticModel, out newSemanticModel);
            var newSymbol = newSemanticModel.GetSymbolInfo(newLambda).Symbol as IMethodSymbol;

            for (int i = 0; i < symbol.Parameters.Length; i++)
            {
                if (symbol.Parameters[i].Type.ToDisplayString() != newSymbol.Parameters[i].Type.ToDisplayString())
                {
                    return;
                }
            }

            foreach (var parameter in lambda.ParameterList.Parameters)
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, parameter.Type.GetLocation(),
                    ImmutableDictionary<string, string>.Empty.Add(DiagnosticTypeKey, RedundancyType.LambdaParameterType.ToString()),
                    "type specification"));
            }
        }

        private static T ChangeSyntaxElement<T>(T originalNode, T newNode, SemanticModel originalSemanticModel,
            out SemanticModel newSemanticModel)
            where T : SyntaxNode
        {
            var annotation = new SyntaxAnnotation();
            var annotatedNode = newNode.WithAdditionalAnnotations(annotation);

            var newSyntaxRoot = originalNode.SyntaxTree.GetRoot().ReplaceNode(
                originalNode,
                annotatedNode);

            var newTree = newSyntaxRoot.SyntaxTree.WithRootAndOptions(
                newSyntaxRoot,
                originalNode.SyntaxTree.Options);

            var newCompilation = originalSemanticModel.Compilation.ReplaceSyntaxTree(
                originalNode.SyntaxTree,
                newTree);

            newSemanticModel = newCompilation.GetSemanticModel(newTree);

            return (T)newTree.GetRoot().GetAnnotatedNodes(annotation).First();
        }

        private static void ReportRedundantNullableConstructorCall(SyntaxNodeAnalysisContext c)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)c.Node;
            if (!IsNullableCreation(objectCreation, c.SemanticModel))
            {
                return;
            }

            if (IsInNotVarDeclaration(objectCreation) ||
                IsInAssignmentOrReturnValue(objectCreation) ||
                IsInArgumentAndCanBeChanged(objectCreation, c.SemanticModel))
            {
                ReportIssueOnRedundantObjectCreation(c, objectCreation, "explicit nullable type creation", RedundancyType.ExplicitNullable);
                return;
            }
        }

        private static bool IsInArgumentAndCanBeChanged(ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel,
            Func<InvocationExpressionSyntax, bool> additionalFilter = null)
        {
            var argument = objectCreation.Parent as ArgumentSyntax;
            if (argument == null)
            {
                return false;
            }

            var invocation = argument.Parent?.Parent as InvocationExpressionSyntax;
            if (invocation == null)
            {
                return false;
            }

            if (additionalFilter != null && additionalFilter(invocation))
            {
                return false;
            }

            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol;
            if (methodSymbol == null)
            {
                return false;
            }

            var newArgumentList = SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(invocation.ArgumentList.Arguments
                    .Select(a => a == argument
                        ? SyntaxFactory.Argument(objectCreation.ArgumentList.Arguments.First().Expression)
                        : a)));
            var newInvocation = invocation.WithArgumentList(newArgumentList);
            SemanticModel newSemanticModel;
            newInvocation = ChangeSyntaxElement(invocation, newInvocation, semanticModel, out newSemanticModel);
            var newMethodSymbol = newSemanticModel.GetSymbolInfo(newInvocation).Symbol as IMethodSymbol;

            return newMethodSymbol != null &&
                methodSymbol.ToDisplayString() == newMethodSymbol.ToDisplayString();
        }

        private static void ReportIssueOnRedundantObjectCreation(SyntaxNodeAnalysisContext c,
            ObjectCreationExpressionSyntax objectCreation, string message, RedundancyType redundancyType)
        {
            var location = Location.Create(objectCreation.SyntaxTree,
                TextSpan.FromBounds(objectCreation.SpanStart, objectCreation.Type.Span.End));
            c.ReportDiagnostic(Diagnostic.Create(Rule, location,
                    ImmutableDictionary<string, string>.Empty.Add(DiagnosticTypeKey, redundancyType.ToString()),
                    message));
        }

        private static void ReportRedundancyInArrayCreation(SyntaxNodeAnalysisContext c)
        {
            var array = (ArrayCreationExpressionSyntax)c.Node;
            ReportRedundantArraySizeSpecifier(c, array);
            ReportRedundantArrayTypeSpecifier(c, array);
        }

        private static void ReportRedundantArraySizeSpecifier(SyntaxNodeAnalysisContext c, ArrayCreationExpressionSyntax array)
        {
            if (array.Initializer != null &&
                array.Type != null)
            {
                var rankSpecifier = array.Type.RankSpecifiers.FirstOrDefault();
                if (rankSpecifier == null ||
                    rankSpecifier.Sizes.Any(s => s.IsKind(SyntaxKind.OmittedArraySizeExpression)))
                {
                    return;
                }

                foreach (var size in rankSpecifier.Sizes)
                {
                    c.ReportDiagnostic(Diagnostic.Create(Rule, size.GetLocation(),
                    ImmutableDictionary<string, string>.Empty.Add(DiagnosticTypeKey, RedundancyType.ArraySize.ToString()),
                    "array size specification"));
                }
            }
        }

        private static void ReportRedundantArrayTypeSpecifier(SyntaxNodeAnalysisContext c, ArrayCreationExpressionSyntax array)
        {
            if (array.Initializer == null ||
                !array.Initializer.Expressions.Any() ||
                array.Type == null)
            {
                return;
            }

            var rankSpecifier = array.Type.RankSpecifiers.FirstOrDefault();
            if (rankSpecifier == null ||
                rankSpecifier.Sizes.Any(s => !s.IsKind(SyntaxKind.OmittedArraySizeExpression)))
            {
                return;
            }

            var arrayType = c.SemanticModel.GetTypeInfo(array.Type).Type as IArrayTypeSymbol;
            if (arrayType == null)
            {
                return;
            }

            var canBeSimplified = array.Initializer.Expressions
                .Select(exp => c.SemanticModel.GetTypeInfo(exp).Type)
                .All(type => object.Equals(type, arrayType.ElementType));

            if (canBeSimplified)
            {
                var location = Location.Create(array.SyntaxTree, TextSpan.FromBounds(
                    array.Type.ElementType.SpanStart, array.Type.RankSpecifiers.Last().SpanStart));

                c.ReportDiagnostic(Diagnostic.Create(Rule, location,
                    ImmutableDictionary<string, string>.Empty.Add(DiagnosticTypeKey, RedundancyType.ArrayType.ToString()),
                    "array type"));
            }
        }

        private static void ReportOnRedundantObjectInitializer(SyntaxNodeAnalysisContext c)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)c.Node;
            if (objectCreation.ArgumentList == null)
            {
                return;
            }

            if (objectCreation.Initializer != null &&
                !objectCreation.Initializer.Expressions.Any())
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.Initializer.GetLocation(),
                    ImmutableDictionary<string, string>.Empty.Add(DiagnosticTypeKey, RedundancyType.ObjectInitializer.ToString()),
                    "initializer"));
            }
        }

        private static void ReportOnExplicitDelegateCreation(SyntaxNodeAnalysisContext c)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)c.Node;
            if (!IsDelegateCreation(objectCreation, c.SemanticModel))
            {
                return;
            }

            if (IsInNotVarDeclaration(objectCreation) ||
                IsInAssignmentOrReturnValue(objectCreation) ||
                IsInArgumentAndCanBeChanged(objectCreation, c.SemanticModel,
                    (invocation) => invocation.ArgumentList.Arguments.Any(a => IsDynamic(a, c.SemanticModel))))
            {
                ReportIssueOnRedundantObjectCreation(c, objectCreation, "explicit delegate creation", RedundancyType.ExplicitDelegate);
                return;
            }
        }

        private static void ReportOnRedundantParameterList(SyntaxNodeAnalysisContext c)
        {
            var anonymousMethod = (AnonymousMethodExpressionSyntax)c.Node;
            if (anonymousMethod.ParameterList == null)
            {
                return;
            }

            var methodSymbol = c.SemanticModel.GetSymbolInfo(anonymousMethod).Symbol as IMethodSymbol;
            if (methodSymbol == null)
            {
                return;
            }

            var parameterNames = methodSymbol.Parameters.Select(p => p.Name).ToImmutableHashSet();

            var usedParameters = anonymousMethod.Body.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(id => parameterNames.Contains(id.Identifier.ValueText))
                .Select(id => c.SemanticModel.GetSymbolInfo(id).Symbol as IParameterSymbol)
                .Where(p => p != null)
                .ToImmutableHashSet();

            if (!usedParameters.Intersect(methodSymbol.Parameters).Any())
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, anonymousMethod.ParameterList.GetLocation(),
                    ImmutableDictionary<string, string>.Empty.Add(DiagnosticTypeKey, RedundancyType.DelegateParameterList.ToString()),
                    "parameter list"));
            }
        }

        private static bool IsNullableCreation(ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel)
        {
            if (objectCreation.ArgumentList == null ||
                objectCreation.ArgumentList.Arguments.Count != 1)
            {
                return false;
            }

            var type = semanticModel.GetSymbolInfo(objectCreation).Symbol?.ContainingType;
            return type != null &&
                type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }

        private static bool IsDelegateCreation(ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel)
        {
            var type = semanticModel.GetSymbolInfo(objectCreation.Type).Symbol as INamedTypeSymbol;

            return type != null &&
                type.TypeKind == TypeKind.Delegate;
        }

        private static bool IsInNotVarDeclaration(ObjectCreationExpressionSyntax objectCreation)
        {
            var variableDeclaration = objectCreation.Parent?.Parent?.Parent as VariableDeclarationSyntax;

            return variableDeclaration != null &&
                variableDeclaration.Type != null &&
                !variableDeclaration.Type.IsVar;
        }

        private static bool IsInAssignmentOrReturnValue(ObjectCreationExpressionSyntax objectCreation)
        {
            return objectCreation.Parent is AssignmentExpressionSyntax ||
                objectCreation.Parent is ReturnStatementSyntax ||
                objectCreation.Parent is LambdaExpressionSyntax;
        }

        private static bool IsDynamic(ArgumentSyntax a, SemanticModel semanticModel)
        {
            return semanticModel.GetTypeInfo(a.Expression).Type is IDynamicTypeSymbol;
        }
    }
}
