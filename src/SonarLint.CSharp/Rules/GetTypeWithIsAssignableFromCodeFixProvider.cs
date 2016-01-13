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
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace SonarLint.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class GetTypeWithIsAssignableFromCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Simplify type checking";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(GetTypeWithIsAssignableFrom.DiagnosticId);
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
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var syntaxNode = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true) as InvocationExpressionSyntax;
            if (syntaxNode == null)
            {
                return;
            }

            var useIsOperator = bool.Parse(diagnostic.Properties[GetTypeWithIsAssignableFrom.UseIsOperatorKey]);

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var newNode = GetRefactoredExpression(syntaxNode, useIsOperator);
                        var newRoot = root.ReplaceNode(syntaxNode, newNode.
                            WithAdditionalAnnotations(Formatter.Annotation));
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }

        private static ExpressionSyntax GetRefactoredExpression(InvocationExpressionSyntax syntaxNode, bool useIsOperator)
        {
            var firstGetTypeExpression = ((MemberAccessExpressionSyntax)syntaxNode.Expression).Expression;
            var firstArgument = syntaxNode.ArgumentList.Arguments.First();

            return useIsOperator
                ? (ExpressionSyntax)SyntaxFactory.BinaryExpression(
                    SyntaxKind.IsExpression,
                    GetExpressionFromGetType(firstGetTypeExpression),
                    ((TypeOfExpressionSyntax)firstArgument.Expression).Type)
                : SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        firstGetTypeExpression,
                        SyntaxFactory.IdentifierName("IsInstanceOfType"))
                        .WithTriviaFrom(syntaxNode.Expression),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Argument(GetExpressionFromGetType(firstArgument.Expression)).WithTriviaFrom(firstArgument)
                        }))
                        .WithTriviaFrom(syntaxNode.ArgumentList))
                    .WithTriviaFrom(syntaxNode);
        }

        private static ExpressionSyntax GetExpressionFromGetType(ExpressionSyntax getTypeCall)
        {
            return ((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)getTypeCall).Expression).Expression;
        }
    }
}
