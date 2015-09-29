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
using Microsoft.CodeAnalysis.Formatting;

namespace SonarLint.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class CatchRethrowCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Remove redundant catch";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(CatchRethrow.DiagnosticId);
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

            var tryStatement = syntaxNode.Parent as TryStatementSyntax;
            if (tryStatement == null)
            {
                return;
            }

            var isTryRemovable = tryStatement.Catches.Count == 1 && tryStatement.Finally == null;
            context.RegisterCodeFix(
                isTryRemovable
                    ? CreateActionWithRemovedTryStatement(context, root, tryStatement)
                    : CreateActionWithRemovedCatchClause(context, root, syntaxNode),
                context.Diagnostics);
        }

        private static CodeAction CreateActionWithRemovedTryStatement(CodeFixContext context, SyntaxNode root, TryStatementSyntax tryStatement)
        {
            return CodeAction.Create(
                Title,
                c =>
                {
                    var newParent = tryStatement.Parent.ReplaceNode(
                        tryStatement,
                        tryStatement.Block.Statements.Select(st => st.WithAdditionalAnnotations(Formatter.Annotation)));

                    var newRoot = root.ReplaceNode(tryStatement.Parent, newParent);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                });
        }

        private static CodeAction CreateActionWithRemovedCatchClause(CodeFixContext context, SyntaxNode root, SyntaxNode syntaxNode)
        {
            return CodeAction.Create(
                Title,
                c =>
                {
                    var newRoot = root.RemoveNode(syntaxNode, SyntaxRemoveOptions.KeepNoTrivia);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                });
        }
    }
}

