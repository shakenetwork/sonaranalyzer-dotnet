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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SonarLint.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class DefaultBaseConstructorCallCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Remove redundant \"base()\" call";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(DefaultBaseConstructorCall.DiagnosticId);
            }
        }
        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override sealed async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var initializer = root.FindNode(diagnosticSpan) as ConstructorInitializerSyntax;
            if (initializer == null)
            {
                return;
            }

            var constructor = initializer.Parent as ConstructorDeclarationSyntax;
            if (constructor == null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var newRoot = RemoveInitializer(root, constructor);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }

        public static SyntaxNode RemoveInitializer(SyntaxNode root, ConstructorDeclarationSyntax constructor)
        {
            var annotation = new SyntaxAnnotation();
            var ctor = constructor;
            var newRoot = root;
            newRoot = newRoot.ReplaceNode(ctor, ctor.WithAdditionalAnnotations(annotation));
            ctor = (ConstructorDeclarationSyntax)newRoot.GetAnnotatedNodes(annotation).First();
            var initializer = ctor.Initializer;

            if (RedundantInheritanceListCodeFixProvider.HasLineEnding(constructor.ParameterList))
            {
                newRoot = newRoot.RemoveNode(initializer, SyntaxRemoveOptions.KeepNoTrivia);
                ctor = (ConstructorDeclarationSyntax)newRoot.GetAnnotatedNodes(annotation).First();

                if (ctor.Body != null &&
                    ctor.Body.HasLeadingTrivia)
                {
                    var lastTrivia = ctor.Body.GetLeadingTrivia().Last();
                    newRoot = lastTrivia.IsKind(SyntaxKind.EndOfLineTrivia)
                        ? newRoot.ReplaceNode(
                            ctor.Body,
                            ctor.Body.WithoutLeadingTrivia())
                        : newRoot.ReplaceNode(
                            ctor.Body,
                            ctor.Body.WithLeadingTrivia(lastTrivia));
                }
            }
            else
            {
                var trailingTrivia = SyntaxFactory.TriviaList();
                if (initializer.HasTrailingTrivia)
                {
                    trailingTrivia = initializer.GetTrailingTrivia();
                }
                newRoot = newRoot.RemoveNode(initializer, SyntaxRemoveOptions.KeepNoTrivia);
                ctor = (ConstructorDeclarationSyntax)newRoot.GetAnnotatedNodes(annotation).First();

                if (ctor.Body != null &&
                    ctor.Body.HasLeadingTrivia)
                {
                    var lastTrivia = ctor.Body.GetLeadingTrivia().Last();
                    newRoot = newRoot.ReplaceNode(
                        ctor.Body,
                        ctor.Body.WithLeadingTrivia(trailingTrivia.Add(lastTrivia)));
                }
                else if (initializer.HasTrailingTrivia)
                {
                    newRoot = newRoot.ReplaceNode(ctor, ctor.WithTrailingTrivia(trailingTrivia));
                }
            }

            ctor = (ConstructorDeclarationSyntax)newRoot.GetAnnotatedNodes(annotation).First();
            return newRoot.ReplaceNode(ctor, ctor.WithoutAnnotations(annotation));
        }
    }
}

