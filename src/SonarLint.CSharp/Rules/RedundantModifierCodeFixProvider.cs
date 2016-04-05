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
using Microsoft.CodeAnalysis.CSharp;
using SonarLint.Common;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace SonarLint.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class RedundantModifierCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Remove redundant modifier";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(RedundantModifier.DiagnosticId);
            }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return DocumentBasedFixAllProvider.Instance;
        }

        private static readonly SyntaxKind[] SimpleTokenKinds =
        {
            SyntaxKind.PartialKeyword,
            SyntaxKind.SealedKeyword
        };

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var token = root.FindToken(diagnosticSpan.Start);

            if (token.IsKind(SyntaxKind.UnsafeKeyword))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        c =>
                        {
                            var newRoot = RemoveRedundantUnsafe(root, token);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        }),
                    context.Diagnostics);
                return;
            }

            if (SimpleTokenKinds.Contains(token.Kind()))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        c =>
                        {
                            var newRoot = RemoveRedundantToken(root, token);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        }),
                    context.Diagnostics);
            }
        }

        private static SyntaxNode RemoveRedundantUnsafe(SyntaxNode root, SyntaxToken token)
        {
            var unsafeStatement = token.Parent as UnsafeStatementSyntax;
            if (unsafeStatement == null)
            {
                return RemoveRedundantToken(root, token);
            }

            var parentBlock = unsafeStatement.Parent as BlockSyntax;
            if (parentBlock != null &&
                parentBlock.Statements.Count == 1)
            {
                return root.ReplaceNode(
                    parentBlock,
                    parentBlock.WithStatements(unsafeStatement.Block.Statements).WithAdditionalAnnotations(Formatter.Annotation));
            }

            return root.ReplaceNode(
                unsafeStatement,
                unsafeStatement.Block.WithAdditionalAnnotations(Formatter.Annotation));
        }

        private static SyntaxNode RemoveRedundantToken(SyntaxNode root, SyntaxToken token)
        {
            var oldParent = token.Parent;
            var newParent = oldParent.ReplaceToken(
                token,
                SyntaxFactory.Token(SyntaxKind.None));

            return root.ReplaceNode(
                oldParent,
                newParent.WithLeadingTrivia(oldParent.GetLeadingTrivia()));
        }
    }
}

