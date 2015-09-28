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
using Microsoft.CodeAnalysis.CSharp;
using SonarLint.Common;

namespace SonarLint.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class IfConditionalAlwaysTrueOrFalseCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Remove useless \"if\" statement";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(IfConditionalAlwaysTrueOrFalse.DiagnosticId);
            }
        }

        private static FixAllProvider FixAllProviderInstance = new DocumentBasedFixAllProvider<IfConditionalAlwaysTrueOrFalse>(
           Title,
           (root, node) => CalculateNewRoot(root, node.FirstAncestorOrSelf<IfStatementSyntax>()));

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return FixAllProviderInstance;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var ifStatement = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<IfStatementSyntax>();

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var newRoot = CalculateNewRoot(root, ifStatement);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }

        private static SyntaxNode CalculateNewRoot(SyntaxNode root, IfStatementSyntax ifStatement)
        {
            SyntaxNode newRoot;
            var isTrue = ifStatement.Condition.IsKind(SyntaxKind.TrueLiteralExpression);

            if (isTrue)
            {
                var block = ifStatement.Statement as BlockSyntax;
                newRoot = block == null
                    ? root.ReplaceNode(ifStatement, ifStatement.Statement.WithAdditionalAnnotations(Formatter.Annotation))
                    : root.ReplaceNode(ifStatement, block.Statements.Select(st => st.WithAdditionalAnnotations(Formatter.Annotation)));
            }
            else
            {
                if (ifStatement.Else == null)
                {
                    newRoot = root.RemoveNode(ifStatement, SyntaxRemoveOptions.KeepNoTrivia);
                }
                else
                {
                    var block = ifStatement.Else.Statement as BlockSyntax;
                    newRoot = block == null
                        ? root.ReplaceNode(ifStatement, ifStatement.Else.Statement.WithAdditionalAnnotations(Formatter.Annotation))
                        : root.ReplaceNode(ifStatement, block.Statements.Select(st => st.WithAdditionalAnnotations(Formatter.Annotation)));
                }
            }

            return newRoot;
        }
    }
}

