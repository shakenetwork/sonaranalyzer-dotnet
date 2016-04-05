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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarLint.Common;

namespace SonarLint.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class RedundancyInConstructorDestructorDeclarationCodeFixProvider : CodeFixProvider
    {
        public const string TitleRemoveBaseCall = "Remove \"base()\" call";
        public const string TitleRemoveConstructor = "Remove constructor";
        public const string TitleRemoveDestructor = "Remove destructor";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(RedundancyInConstructorDestructorDeclaration.DiagnosticId);
            }
        }
        public sealed override FixAllProvider GetFixAllProvider() => DocumentBasedFixAllProvider.Instance;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var syntaxNode = root.FindNode(diagnosticSpan);
            var initializer = syntaxNode as ConstructorInitializerSyntax;
            if (initializer != null)
            {
                RegisterActionForBaseCall(context, root, initializer);
                return;
            }

            var method = syntaxNode.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();

            if (method is ConstructorDeclarationSyntax)
            {
                RegisterActionForConstructor(context, root, method);
                return;
            }

            if (method is DestructorDeclarationSyntax)
            {
                RegisterActionForDestructor(context, root, method);
            }
        }

        private static void RegisterActionForDestructor(CodeFixContext context, SyntaxNode root, BaseMethodDeclarationSyntax method)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    TitleRemoveDestructor,
                    c =>
                    {
                        var newRoot = root.RemoveNode(
                            method,
                            SyntaxRemoveOptions.KeepNoTrivia);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    TitleRemoveDestructor),
                context.Diagnostics);
        }

        private static void RegisterActionForConstructor(CodeFixContext context, SyntaxNode root, BaseMethodDeclarationSyntax method)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    TitleRemoveConstructor,
                    c =>
                    {
                        var newRoot = root.RemoveNode(
                            method,
                            SyntaxRemoveOptions.KeepNoTrivia);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    TitleRemoveConstructor),
                context.Diagnostics);
        }

        private static void RegisterActionForBaseCall(CodeFixContext context, SyntaxNode root, ConstructorInitializerSyntax initializer)
        {
            var constructor = initializer.Parent as ConstructorDeclarationSyntax;
            if (constructor == null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    TitleRemoveBaseCall,
                    c =>
                    {
                        var newRoot = RemoveInitializer(root, constructor);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    TitleRemoveBaseCall),
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
                else
                {
                    if (initializer.HasTrailingTrivia)
                    {
                        newRoot = newRoot.ReplaceNode(ctor, ctor.WithTrailingTrivia(trailingTrivia));
                    }
                }
            }

            ctor = (ConstructorDeclarationSyntax)newRoot.GetAnnotatedNodes(annotation).First();
            return newRoot.ReplaceNode(ctor, ctor.WithoutAnnotations(annotation));
        }
    }
}

