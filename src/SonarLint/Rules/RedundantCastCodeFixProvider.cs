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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SonarLint.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class RedundantCastCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Remove redundant cast";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(RedundantCast.DiagnosticId);
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
            var syntaxNode = root.FindNode(diagnosticSpan);

            var castExpression = syntaxNode.Parent as CastExpressionSyntax;

            if (castExpression != null)
            {
                //this is handled by IDE0004 code fix.
                return;
            }

            var castInvocation = syntaxNode as InvocationExpressionSyntax;
            if (castInvocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        c => RemoveExtensionMethodCall(context.Document, root, castInvocation)),
                    context.Diagnostics);
                return;
            }

            var memberAccess = syntaxNode as MemberAccessExpressionSyntax;
            if (memberAccess != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        c => RemoveStaticMemberCall(context.Document, root, memberAccess)),
                    context.Diagnostics);
                return;
            }
        }

        private static Task<Document> RemoveStaticMemberCall(Document document, SyntaxNode root,
            MemberAccessExpressionSyntax memberAccess)
        {
            var invocation = (InvocationExpressionSyntax)memberAccess.Parent;
            var newRoot = root.ReplaceNode(invocation, invocation.ArgumentList.Arguments.First().Expression);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        private static Task<Document> RemoveExtensionMethodCall(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        {
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
            var newRoot = root.ReplaceNode(invocation, memberAccess.Expression);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }
    }
}

