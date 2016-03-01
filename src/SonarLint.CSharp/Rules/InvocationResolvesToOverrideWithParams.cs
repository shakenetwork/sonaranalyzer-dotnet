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
using System.Linq;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class InvocationResolvesToOverrideWithParams : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3220";
        internal const string Title = "Method calls should not resolve ambiguously to overloads with \"params\"";
        internal const string Description =
            "The rules for method resolution are complex and perhaps not properly understood by all coders. The \"params\" keyword can make " +
            "method declarations overlap in non-obvious ways, so that slight changes in the argument types of an invocation can resolve to " +
            "different methods. This rule raises an issue when an invocation resolves to a method declaration with \"params\", but could also " +
            "resolve to another non-\"params\" method too.";
        internal const string MessageFormat = "Review this call, which partially matches an overload without \"params\". The partial match is \"{0}\".";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax)c.Node;
                    CheckCall(invocation, invocation.ArgumentList, c);
                },
                SyntaxKind.InvocationExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var objectCreation = (ObjectCreationExpressionSyntax)c.Node;
                    CheckCall(objectCreation, objectCreation.ArgumentList, c);
                },
                SyntaxKind.ObjectCreationExpression);
        }

        private static void CheckCall(SyntaxNode node, ArgumentListSyntax argumentList, SyntaxNodeAnalysisContext context)
        {
            var invokedMethodSymbol = context.SemanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
            if (invokedMethodSymbol == null ||
                !invokedMethodSymbol.Parameters.Any() ||
                !invokedMethodSymbol.Parameters.Last().IsParams ||
                argumentList == null)
            {
                return;
            }

            if (IsInvocationWithExplicitArray(argumentList, invokedMethodSymbol, context.SemanticModel))
            {
                return;
            }

            var argumentTypes = argumentList.Arguments
                .Select(arg => context.SemanticModel.GetTypeInfo(arg.Expression).Type)
                .ToList();
            if (argumentTypes.Any(type => type is IErrorTypeSymbol))
            {
                return;
            }

            var possibleOtherMethods = invokedMethodSymbol.ContainingType.GetMembers(invokedMethodSymbol.Name)
                .OfType<IMethodSymbol>()
                .Where(m => !m.IsVararg)
                .Where(m => m.MethodKind == invokedMethodSymbol.MethodKind)
                .Where(m => !invokedMethodSymbol.Equals(m))
                .Where(m => m.Parameters.Any() && !m.Parameters.Last().IsParams);

            var otherMethod = possibleOtherMethods.FirstOrDefault(possibleOtherMethod =>
                    ArgumentsMatchParameters(
                        argumentList,
                        argumentTypes.Select(t => t as INamedTypeSymbol).ToList(),
                        possibleOtherMethod,
                        context.SemanticModel));

            if (otherMethod != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    node.GetLocation(),
                    otherMethod.ToMinimalDisplayString(context.SemanticModel, node.SpanStart)));
            }
        }

        private static bool IsInvocationWithExplicitArray(ArgumentListSyntax argumentList, IMethodSymbol invokedMethodSymbol,
            SemanticModel semanticModel)
        {
            var allParameterMatches = new List<IParameterSymbol>();
            foreach (var argument in argumentList.Arguments)
            {
                IParameterSymbol parameter;
                if (!MethodParameterLookup.TryGetParameterSymbol(argument, argumentList, invokedMethodSymbol, out parameter))
                {
                    return false;
                }
                allParameterMatches.Add(parameter);
                if (parameter.IsParams)
                {
                    var argType = semanticModel.GetTypeInfo(argument.Expression).Type;
                    if (!(argType is IArrayTypeSymbol))
                    {
                        return false;
                    }
                }
            }

            return allParameterMatches.Count(p => p.IsParams) == 1;
        }

        private static bool ArgumentsMatchParameters(ArgumentListSyntax argumentList, List<INamedTypeSymbol> argumentTypes,
            IMethodSymbol possibleOtherMethod, SemanticModel semanticModel)
        {
            var matchedParameters = new List<IParameterSymbol>();
            for (int i = 0; i < argumentList.Arguments.Count; i++)
            {
                var argument = argumentList.Arguments[i];
                var argumentType = argumentTypes[i];
                IParameterSymbol parameter;
                if (!MethodParameterLookup.TryGetParameterSymbol(argument, argumentList, possibleOtherMethod, out parameter))
                {
                    return false;
                }

                if (argumentType == null)
                {
                    if (!parameter.Type.IsReferenceType)
                    {
                        return false;
                    }
                }
                else
                {
                    var conversion = semanticModel.ClassifyConversion(argument.Expression, parameter.Type);
                    if (!conversion.IsImplicit)
                    {
                        return false;
                    }
                }

                matchedParameters.Add(parameter);
            }

            var nonMatchedParameters = possibleOtherMethod.Parameters.Except(matchedParameters);
            return nonMatchedParameters.All(p => p.HasExplicitDefaultValue);
        }
    }
}
