/*
 * SonarAnalyzer for .NET
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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using SonarAnalyzer.Helpers.FlowAnalysis.CSharp;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    using LiveVariableAnalysis = Helpers.FlowAnalysis.CSharp.LiveVariableAnalysis;

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
        internal const string MessageFormat = "Remove this {0}.";
        internal const string MessageUnused = "unused method parameter \"{0}\"";
        internal const string MessageDead = "parameter \"{0}\", whose value is ignored in the method";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
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

        internal const string IsRemovableKey = "IsRemovable";

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (BaseMethodDeclarationSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(declaration);
                    if (symbol == null ||
                        !symbol.ContainingType.IsClassOrStruct())
                    {
                        return;
                    }

                    ReportUnusedParametersOnMethod(declaration, symbol, c);
                },
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ConstructorDeclaration);
        }

        private static void ReportUnusedParametersOnMethod(BaseMethodDeclarationSyntax declaration, IMethodSymbol methodSymbol,
            SyntaxNodeAnalysisContext context)
        {
            if (!MethodCanBeSafelyChanged(methodSymbol))
            {
                return;
            }

            var unusedParameters = GetUnusedParameters(declaration, methodSymbol, context.SemanticModel);
            if (unusedParameters.Any() &&
                !IsUsedAsEventHandlerFunctionOrAction(methodSymbol, context.SemanticModel.Compilation))
            {
                ReportOnUnusedParameters(declaration, unusedParameters, MessageUnused, context);
            }

            ReportOnDeadParametersAtEntry(declaration, methodSymbol, unusedParameters, context);
        }

        private static void ReportOnDeadParametersAtEntry(BaseMethodDeclarationSyntax declaration, IMethodSymbol methodSymbol,
            IImmutableList<IParameterSymbol> noReportOnParameters, SyntaxNodeAnalysisContext context)
        {
            if (!declaration.IsKind(SyntaxKind.MethodDeclaration) ||
                declaration.Body == null)
            {
                return;
            }

            var excludedParameters = noReportOnParameters;
            if (methodSymbol.IsExtensionMethod)
            {
                excludedParameters = excludedParameters.Add(methodSymbol.Parameters.First());
            }

            excludedParameters = excludedParameters.AddRange(methodSymbol.Parameters.Where(p => p.RefKind != RefKind.None));

            var candidateParameters = methodSymbol.Parameters.Except(excludedParameters);
            if (!candidateParameters.Any())
            {
                return;
            }

            IControlFlowGraph cfg;
            if (!ControlFlowGraph.TryGet(declaration.Body, context.SemanticModel, out cfg))
            {
                return;
            }

            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, context.SemanticModel);
            var liveParameters = lva.GetLiveIn(cfg.EntryBlock).OfType<IParameterSymbol>();

            ReportOnUnusedParameters(declaration, candidateParameters.Except(liveParameters).Except(lva.CapturedVariables), MessageDead,
                context, isRemovable: false);
        }

        private static void ReportOnUnusedParameters(BaseMethodDeclarationSyntax declaration, IEnumerable<ISymbol> parametersToReportOn,
            string messagePattern, SyntaxNodeAnalysisContext context, bool isRemovable = true)
        {
            if (declaration.ParameterList == null)
            {
                return;
            }

            var parameters = declaration.ParameterList.Parameters
                .Select(p => new
                {
                    Syntax = p,
                    Symbol = context.SemanticModel.GetDeclaredSymbol(p)
                })
                .Where(p => p.Symbol != null);

            foreach (var parameter in parameters)
            {
                if (parametersToReportOn.Contains(parameter.Symbol))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, parameter.Syntax.GetLocation(),
                        ImmutableDictionary<string, string>.Empty.Add(IsRemovableKey, isRemovable.ToString()),
                        string.Format(messagePattern, parameter.Symbol.Name)));
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

        private static IImmutableList<IParameterSymbol> GetUnusedParameters(BaseMethodDeclarationSyntax declaration, IMethodSymbol methodSymbol,
            SemanticModel semanticModel)
        {
            var usedParameters = new HashSet<IParameterSymbol>();

            SyntaxNode[] bodies;

            if (declaration.IsKind(SyntaxKind.MethodDeclaration))
            {
                var methodDeclararion = (MethodDeclarationSyntax)declaration;
                bodies = new SyntaxNode[] { methodDeclararion.Body, methodDeclararion.ExpressionBody };
            }
            else
            {
                var constructorDeclaration = (ConstructorDeclarationSyntax)declaration;
                bodies = new SyntaxNode[] { constructorDeclaration.Body, constructorDeclaration.Initializer };
            }

            foreach (var body in bodies.Where(b => b != null))
            {
                usedParameters.UnionWith(GetUsedParameters(methodSymbol.Parameters, body, semanticModel));
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
            return methodSymbol.ContainingType.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax())
                .Any(n => IsMethodUsedAsEventHandlerFunctionOrActionWithinNode(methodSymbol, n, compilation.GetSemanticModel(n.SyntaxTree)));
        }

        private static bool IsMethodUsedAsEventHandlerFunctionOrActionWithinNode(IMethodSymbol methodSymbol, SyntaxNode typeDeclaration, SemanticModel semanticModel)
        {
            return typeDeclaration.DescendantNodes()
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
