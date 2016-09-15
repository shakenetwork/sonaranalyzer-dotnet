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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cert, Tag.Design, Tag.Unused)]
    public class UnusedReturnValue : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3241";
        internal const string Title = "Methods should not return values that are never used";
        internal const string Description =
            "Private methods are clearly intended for use only within their own scope. When such methods return values that are never used by any of their callers, then " +
            "clearly there is no need to actually make the return, and it should be removed in the interests of efficiency and clarity.";
        internal const string MessageFormat = "Change return type to \"void\"; not a single caller uses the returned value.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
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
            context.RegisterSymbolAction(
                c =>
                {
                    var namedType = (INamedTypeSymbol)c.Symbol;
                    if (!namedType.IsClassOrStruct() ||
                        namedType.ContainingType != null)
                    {
                        return;
                    }

                    var removableDeclarationCollector = new RemovableDeclarationCollector(namedType, c.Compilation);

                    var declaredPrivateMethodsWithReturn = CollectRemovableMethods(removableDeclarationCollector).ToList();
                    if (!declaredPrivateMethodsWithReturn.Any())
                    {
                        return;
                    }

                    var invocations = CollectInvocations(removableDeclarationCollector.TypeDeclarations).ToList();

                    foreach (var declaredPrivateMethodWithReturn in declaredPrivateMethodsWithReturn)
                    {
                        var matchingInvocations = invocations
                            .Where(inv => object.Equals(inv.Symbol.OriginalDefinition, declaredPrivateMethodWithReturn.Symbol))
                            .ToList();

                        if (!matchingInvocations.Any())
                        {
                            /// this is handled by S1144 <see cref="UnusedPrivateMember"/>
                            continue;
                        }

                        if (!IsReturnValueUsed(matchingInvocations))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, declaredPrivateMethodWithReturn.SyntaxNode.ReturnType.GetLocation()));
                        }
                    }
                },
                SymbolKind.NamedType);
        }

        private static bool IsReturnValueUsed(IEnumerable<SyntaxNodeSymbolSemanticModelTuple<InvocationExpressionSyntax, IMethodSymbol>> matchingInvocations)
        {
            return matchingInvocations.Any(invocation =>
                !IsExpressionStatement(invocation.SyntaxNode.Parent) &&
                !IsActionLambda(invocation.SyntaxNode.Parent, invocation.SemanticModel));
        }

        private static bool IsActionLambda(SyntaxNode node, SemanticModel semanticModel)
        {
            var lambda = node as LambdaExpressionSyntax;
            if (lambda == null)
            {
                return false;
            }

            var symbol = semanticModel.GetSymbolInfo(lambda).Symbol as IMethodSymbol;
            return symbol != null && symbol.ReturnsVoid;
        }

        private static bool IsExpressionStatement(SyntaxNode node)
        {
            return node is ExpressionStatementSyntax;
        }

        private static IEnumerable<SyntaxNodeSymbolSemanticModelTuple<InvocationExpressionSyntax, IMethodSymbol>> CollectInvocations(
            IEnumerable<SyntaxNodeSemanticModelTuple<BaseTypeDeclarationSyntax>> containers)
        {
            return containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Select(node =>
                        new SyntaxNodeSymbolSemanticModelTuple<InvocationExpressionSyntax, IMethodSymbol>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel,
                            Symbol = container.SemanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol
                        }))
                    .Where(invocation => invocation.Symbol != null);
        }

        private static IEnumerable<SyntaxNodeSymbolSemanticModelTuple<MethodDeclarationSyntax, IMethodSymbol>> CollectRemovableMethods(
            RemovableDeclarationCollector removableDeclarationCollector)
        {
            return removableDeclarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes(RemovableDeclarationCollector.IsNodeContainerTypeDeclaration)
                    .OfType<MethodDeclarationSyntax>()
                    .Select(node =>
                        new SyntaxNodeSymbolSemanticModelTuple<MethodDeclarationSyntax, IMethodSymbol>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel,
                            Symbol = container.SemanticModel.GetDeclaredSymbol(node)
                        }))
                    .Where(node =>
                        node.Symbol != null &&
                        !node.Symbol.ReturnsVoid &&
                        RemovableDeclarationCollector.IsRemovable(node.Symbol, Accessibility.Private));
        }
    }
}
