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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Collections.Generic;
using System;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Misra, Tag.Unused)]
    public class MethodParameterUnused : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1172";
        internal const string Title = "Unused method parameters should be removed";
        internal const string Description =
            "Unused parameters are misleading. Whatever the value passed to such parameters is, the behavior will be the same.";
        internal const string MessageFormat = "Remove this unused method parameter \"{0}\".";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var namedType = (INamedTypeSymbol)c.Symbol;
                    if (!namedType.IsClassOrStruct())
                    {
                        return;
                    }

                    foreach (var methodSymbol in namedType.GetMembers().OfType<IMethodSymbol>())
                    {
                        ReportUnusedParametersOnMethod(methodSymbol, c);
                    }
                },
                SymbolKind.NamedType);
        }

        private static void ReportUnusedParametersOnMethod(IMethodSymbol methodSymbol, SymbolAnalysisContext context)
        {
            if (!MethodCanBeSafelyChanged(methodSymbol))
            {
                return;
            }

            var unusedParameters = GetUnusedParameters(methodSymbol, context.Compilation);
            if (!unusedParameters.Any() || IsUsedAsEventHandlerFunctionOrAction(methodSymbol, context.Compilation))
            {
                return;
            }

            foreach (var unusedParameter in unusedParameters)
            {
                foreach (var unusedParameterDeclaration in unusedParameter.DeclaringSyntaxReferences.Select(r => r.GetSyntax()))
                {
                    context.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule, unusedParameterDeclaration.GetLocation(), unusedParameter.Name));
                }
            }
        }

        private static bool MethodCanBeSafelyChanged(IMethodSymbol methodSymbol)
        {
            return methodSymbol.DeclaredAccessibility == Accessibility.Private &&
                !methodSymbol.GetAttributes().Any() &&
                methodSymbol.IsChangeable() &&
                !methodSymbol.IsProbablyEventHandler();
        }

        private static IImmutableList<IParameterSymbol> GetUnusedParameters(IMethodSymbol methodSymbol, Compilation compilation)
        {
            var usedParameters = new HashSet<IParameterSymbol>();

            var bodies = methodSymbol.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax())
                .Where(n => n.IsKind(SyntaxKind.MethodDeclaration) || n.IsKind(SyntaxKind.ConstructorDeclaration))
                .SelectMany(
                    n =>
                    {
                        if (n.IsKind(SyntaxKind.MethodDeclaration))
                        {
                            var methodDeclararion = (MethodDeclarationSyntax)n;
                            return new SyntaxNode[] { methodDeclararion.Body, methodDeclararion.ExpressionBody };
                        }
                        else if (n.IsKind(SyntaxKind.ConstructorDeclaration))
                        {
                            var constructorDeclaration = (ConstructorDeclarationSyntax)n;
                            return new SyntaxNode[] { constructorDeclaration.Body, constructorDeclaration.Initializer };
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    });

            foreach (var body in bodies.Where(b => b != null))
            {
                usedParameters.UnionWith(GetUsedParameters(methodSymbol.Parameters, body, compilation.GetSemanticModel(body.SyntaxTree)));
            }

            var unusedParameter = methodSymbol.Parameters.Except(usedParameters);
            if (methodSymbol.IsExtensionMethod)
            {
                unusedParameter = unusedParameter.Except(new[] { methodSymbol.Parameters.First() });
            }

            return unusedParameter.Except(usedParameters).ToImmutableArray();
        }

        private static IImmutableSet<IParameterSymbol> GetUsedParameters(ImmutableArray<IParameterSymbol> parameters, SyntaxNode body, SemanticModel semanticModel)
        {
            return body.DescendantNodes()
                       .Where(n => n.IsKind(SyntaxKind.IdentifierName))
                       .Select(identierName => semanticModel.GetSymbolInfo(identierName).Symbol as IParameterSymbol)
                       .Where(symbol => symbol != null && parameters.Contains(symbol))
                       .ToImmutableHashSet();
        }

        private static bool IsUsedAsEventHandlerFunctionOrAction(IMethodSymbol methodSymbol, Compilation compilation)
        {
            return methodSymbol
                .ContainingType
                .DeclaringSyntaxReferences
                .Select(r => r.GetSyntax())
                .Any(n => IsMethodUsedAsEventHandlerFunctionOrActionWithinNode(methodSymbol, n, compilation.GetSemanticModel(n.SyntaxTree)));
        }

        private static bool IsMethodUsedAsEventHandlerFunctionOrActionWithinNode(IMethodSymbol methodSymbol, SyntaxNode syntaxNode, SemanticModel semanticModel)
        {
            return syntaxNode
                .DescendantNodes()
                .OfType<ExpressionSyntax>()
                .Any(n => IsMethodUsedAsEventHandlerFunctionOrActionInExpression(methodSymbol, n, semanticModel));
        }

        private static bool IsMethodUsedAsEventHandlerFunctionOrActionInExpression(IMethodSymbol methodSymbol, ExpressionSyntax expression, SemanticModel semanticModel)
        {
            return !expression.IsKind(SyntaxKind.InvocationExpression) &&
                IsStandaloneExpression(expression) &&
                methodSymbol.Equals(semanticModel.GetSymbolInfo(expression).Symbol?.OriginalDefinition);
        }

        private static bool IsStandaloneExpression(ExpressionSyntax expression)
        {
            var parentAsAssignment = expression.Parent as AssignmentExpressionSyntax;

            return !(expression.Parent is ExpressionSyntax) ||
                (parentAsAssignment != null && object.ReferenceEquals(expression, parentAsAssignment.Right));
        }
    }
}
