/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SonarLint.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class RedundantArgumentCodeFixProvider : CodeFixProvider
    {
        public const string TitleRemove = "Remove redundant arguments";
        public const string TitleRemoveWithNameAdditions = "Remove redundant arguments with adding named arguments";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(RedundantArgument.DiagnosticId);
            }
        }
        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var invocation = GetInvocation(root, diagnostic.Location.SourceSpan);
            if (invocation == null)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
            var methodParameterLookup = new ArrayCovariance.MethodParameterLookup(invocation, semanticModel);
            var argumentMappings = invocation.ArgumentList.Arguments
                .Select(argument =>
                    new KeyValuePair<ArgumentSyntax, IParameterSymbol>(argument,
                        methodParameterLookup.GetParameterSymbol(argument)))
                .ToList();

            var methodSymbol = methodParameterLookup.MethodSymbol;
            if (methodSymbol == null)
            {
                return;
            }

            var argumentsWithDefaultValues = new List<ArgumentSyntax>();
            var argumentsCanBeRemovedWithoutNamed = new List<ArgumentSyntax>();
            var canBeRemovedWithoutNamed = true;

            var reversedMappings =
                ((IEnumerable<KeyValuePair<ArgumentSyntax, IParameterSymbol>>) argumentMappings).Reverse();
            foreach (var argumentMapping in reversedMappings)
            {
                var argument = argumentMapping.Key;

                if (RedundantArgument.ArgumentHasDefaultValue(argumentMapping, semanticModel))
                {
                    argumentsWithDefaultValues.Add(argument);

                    if (canBeRemovedWithoutNamed)
                    {
                        argumentsCanBeRemovedWithoutNamed.Add(argument);
                    }
                }
                else if (argument.NameColon == null)
                {
                    canBeRemovedWithoutNamed = false;
                }
            }

            if (argumentsCanBeRemovedWithoutNamed.Any())
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        TitleRemove,
                        c => RemoveArguments(context.Document, argumentsCanBeRemovedWithoutNamed, c),
                        TitleRemove),
                    context.Diagnostics);
            }

            var cannotBeRemoved = argumentsWithDefaultValues.Except(argumentsCanBeRemovedWithoutNamed);
            if (cannotBeRemoved.Any())
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        TitleRemoveWithNameAdditions,
                        c => RemoveArgumentsAndAddNecessaryNames(context.Document, invocation.ArgumentList,
                                argumentMappings, argumentsWithDefaultValues, semanticModel, c),
                        TitleRemoveWithNameAdditions),
                    context.Diagnostics);
            }
        }

        private static async Task<Document> RemoveArgumentsAndAddNecessaryNames(Document document, ArgumentListSyntax argumentList,
            List<KeyValuePair<ArgumentSyntax, IParameterSymbol>> argumentMappings, List<ArgumentSyntax> argumentsToRemove,
            SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newArgumentList = SyntaxFactory.ArgumentList();
            var alreadyRemovedOne = false;

            foreach (var argumentMapping in argumentMappings
                .Where(argumentMapping => !argumentMapping.Value.IsParams))
            {
                var argument = argumentMapping.Key;
                if (argumentsToRemove.Contains(argument))
                {
                    alreadyRemovedOne = true;
                    continue;
                }

                newArgumentList = AddArgument(alreadyRemovedOne, newArgumentList, argumentMapping.Value.Name, argument);
            }

            var paramsArguments = argumentMappings
                .Where(mapping => mapping.Value.IsParams)
                .ToList();

            if (paramsArguments.Any())
            {
                newArgumentList = AddParamsArguments(semanticModel, paramsArguments, newArgumentList);
            }

            var newRoot = root.ReplaceNode(argumentList, newArgumentList);
            return document.WithSyntaxRoot(newRoot);
        }

        private static ArgumentListSyntax AddArgument(bool alreadyRemovedOne, ArgumentListSyntax argumentList,
            string parameterName, ArgumentSyntax argument)
        {
            return alreadyRemovedOne
                ? argumentList.AddArguments(
                    SyntaxFactory.Argument(
                        SyntaxFactory.NameColon(
                            SyntaxFactory.IdentifierName(parameterName)),
                        argument.RefOrOutKeyword,
                        argument.Expression))
                : argumentList.AddArguments(argument);
        }

        private static ArgumentListSyntax AddParamsArguments(SemanticModel semanticModel,
            List<KeyValuePair<ArgumentSyntax, IParameterSymbol>> paramsArguments, ArgumentListSyntax argumentList)
        {
            var firstParamsMapping = paramsArguments.First();
            var firstParamsArgument = firstParamsMapping.Key;
            var paramsParameter = firstParamsMapping.Value;

            if (firstParamsArgument.NameColon != null)
            {
                return argumentList.AddArguments(firstParamsArgument);
            }

            if (paramsArguments.Count == 1 &&
                paramsParameter.Type.Equals(
                    semanticModel.GetTypeInfo(firstParamsArgument.Expression).Type))
            {
                return argumentList.AddArguments(
                    SyntaxFactory.Argument(
                        SyntaxFactory.NameColon(
                            SyntaxFactory.IdentifierName(paramsParameter.Name)),
                        firstParamsArgument.RefOrOutKeyword,
                        firstParamsArgument.Expression));
            }

            return argumentList.AddArguments(
                SyntaxFactory.Argument(
                    SyntaxFactory.NameColon(
                        SyntaxFactory.IdentifierName(paramsParameter.Name)),
                    SyntaxFactory.Token(SyntaxKind.None),
                    SyntaxFactory.ImplicitArrayCreationExpression(
                        SyntaxFactory.InitializerExpression(
                            SyntaxKind.ArrayInitializerExpression,
                            SyntaxFactory.SeparatedList(
                                paramsArguments.Select(arg => arg.Key.Expression))
                            ))));
        }

        private static async Task<Document> RemoveArguments(Document document, List<ArgumentSyntax> arguments,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.RemoveNodes(arguments, SyntaxRemoveOptions.KeepNoTrivia | SyntaxRemoveOptions.AddElasticMarker);
            return document.WithSyntaxRoot(newRoot);
        }

        private static InvocationExpressionSyntax GetInvocation(SyntaxNode root, TextSpan diagnosticSpan)
        {
            var argumentSyntax = root.FindNode(diagnosticSpan) as ArgumentSyntax;
            if (argumentSyntax == null)
            {
                return null;
            }

            return argumentSyntax.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        }
    }
}
