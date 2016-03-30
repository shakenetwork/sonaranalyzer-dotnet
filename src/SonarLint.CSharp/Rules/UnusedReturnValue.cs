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
using System.Collections.Generic;
using System.Linq;

namespace SonarLint.Rules.CSharp
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
            context.RegisterSymbolAction(
                c =>
                {
                    var namedType = (INamedTypeSymbol)c.Symbol;
                    if (!namedType.IsClassOrStruct() ||
                        namedType.ContainingType != null)
                    {
                        return;
                    }

                    var classDeclarations = UnusedPrivateMember.GetClassDeclarations(namedType, c);

                    var declaredPrivateMethodsWithReturn = CollectRemovableMethods(classDeclarations).ToList();
                    if (!declaredPrivateMethodsWithReturn.Any())
                    {
                        return;
                    }

                    var invocations = CollectInvocations(classDeclarations).ToList();

                    foreach (var declaredPrivateMethodWithReturn in declaredPrivateMethodsWithReturn)
                    {
                        var matchingInvocations = invocations
                            .Where(inv => object.Equals(inv.MethodSymbol.OriginalDefinition, declaredPrivateMethodWithReturn.MethodSymbol))
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

        private static bool IsReturnValueUsed(IEnumerable<SyntaxWithSymbol<InvocationExpressionSyntax>> matchingInvocations)
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

        private static IEnumerable<SyntaxWithSymbol<InvocationExpressionSyntax>> CollectInvocations(
            IEnumerable<SyntaxNodeWithSemanticModel<ClassDeclarationSyntax>> containers)
        {
            return containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Select(node =>
                        new SyntaxWithSymbol<InvocationExpressionSyntax>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel,
                            MethodSymbol = container.SemanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol
                        }))
                    .Where(invocation => invocation.MethodSymbol != null);
        }

        private static IEnumerable<SyntaxWithSymbol<MethodDeclarationSyntax>> CollectRemovableMethods(
            IEnumerable<SyntaxNodeWithSemanticModel<ClassDeclarationSyntax>> containers)
        {
            return containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes(UnusedPrivateMember.IsNodeContainerTypeDeclaration)
                    .OfType<MethodDeclarationSyntax>()
                    .Select(node =>
                        new SyntaxWithSymbol<MethodDeclarationSyntax>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel,
                            MethodSymbol = container.SemanticModel.GetDeclaredSymbol(node) as IMethodSymbol
                        }))
                    .Where(node =>
                        node.MethodSymbol != null &&
                        !node.MethodSymbol.ReturnsVoid &&
                        UnusedPrivateMember.IsMethodSymbolQualifyingPrivate(node.MethodSymbol));
        }

        private class SyntaxWithSymbol<T> : SyntaxNodeWithSemanticModel<T> where T : SyntaxNode
        {
            public IMethodSymbol MethodSymbol { get; set; }
        }
    }
}
