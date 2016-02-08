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
using SonarLint.Common;
using Microsoft.CodeAnalysis.CSharp;

namespace SonarLint.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class RedundantPropertyNamesInAnonymousClassCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Remove redundant explicit property names";
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RedundantPropertyNamesInAnonymousClass.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => DocumentBasedFixAllProvider.Instance;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var nameEquals = root.FindNode(diagnosticSpan) as NameEqualsSyntax;
            var anonymousObjectCreation = nameEquals?.Parent?.Parent as AnonymousObjectCreationExpressionSyntax;
            if (anonymousObjectCreation == null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var newInitializersWithSeparators = anonymousObjectCreation.Initializers.GetWithSeparators()
                            .Select(item => GetNewSyntaxListItem(item));
                        var newAnonymousObjectCreation = anonymousObjectCreation
                            .WithInitializers(SyntaxFactory.SeparatedList<AnonymousObjectMemberDeclaratorSyntax>(newInitializersWithSeparators))
                            .WithTriviaFrom(anonymousObjectCreation);

                        var newRoot = root.ReplaceNode(
                            anonymousObjectCreation,
                            newAnonymousObjectCreation);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }

        private static SyntaxNodeOrToken GetNewSyntaxListItem(SyntaxNodeOrToken item)
        {
            if (item.IsNode)
            {
                var member = (AnonymousObjectMemberDeclaratorSyntax)item.AsNode();
                var identifier = member.Expression as IdentifierNameSyntax;
                if (identifier != null &&
                    identifier.Identifier.ValueText == member.NameEquals.Name.Identifier.ValueText)
                {
                    return SyntaxFactory.AnonymousObjectMemberDeclarator(member.Expression).WithTriviaFrom(member);
                }
            }

            return item;
        }
    }
}

