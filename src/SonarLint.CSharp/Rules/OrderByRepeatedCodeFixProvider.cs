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
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;

namespace SonarLint.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class OrderByRepeatedCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Change \"OrderBy\" to \"ThenBy\"";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(OrderByRepeated.DiagnosticId);
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

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c => ChangeToThenByAsync(context.Document, syntaxNode, c)),
                context.Diagnostics);
        }

        private static async Task<Document> ChangeToThenByAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(syntaxNode,
                SyntaxFactory.IdentifierName("ThenBy"));
            return document.WithSyntaxRoot(newRoot);
        }
    }
}

