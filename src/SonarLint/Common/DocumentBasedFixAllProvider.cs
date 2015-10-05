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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using System;

namespace SonarLint.Common
{
    internal sealed class DocumentBasedFixAllProvider<T> : FixAllProvider
    {
        private readonly string title;
        private readonly Func<SyntaxNode, SyntaxNode, SyntaxNode> calculateNewRoot;
        internal static readonly SyntaxAnnotation annotation = new SyntaxAnnotation(typeof(T).Name);

        public DocumentBasedFixAllProvider(string title, Func<SyntaxNode, SyntaxNode, SyntaxNode> calculateNewRoot)
        {
            this.title = title;
            this.calculateNewRoot = calculateNewRoot;
        }

        public override Task<CodeAction> GetFixAsync(FixAllContext fixAllContext)
        {
            switch (fixAllContext.Scope)
            {
                case FixAllScope.Document:
                    return Task.FromResult(CodeAction.Create(title,
                        async ct => fixAllContext.Document.WithSyntaxRoot(
                            await GetFixedDocument(fixAllContext, fixAllContext.Document).ConfigureAwait(false))));
                case FixAllScope.Project:
                    return Task.FromResult(CodeAction.Create(title,
                        ct => GetFixedProject(fixAllContext, fixAllContext.Project)));
                case FixAllScope.Solution:
                    return Task.FromResult(CodeAction.Create(title,
                        ct => GetFixedSolution(fixAllContext)));
                default:
                    return null;
            }
        }

        private async Task<Solution> GetFixedSolution(FixAllContext fixAllContext)
        {
            var newSolution = fixAllContext.Solution;
            foreach (var projectId in newSolution.ProjectIds)
            {
                newSolution = await GetFixedProject(fixAllContext, newSolution.GetProject(projectId))
                    .ConfigureAwait(false);
            }
            return newSolution;
        }

        private async Task<Solution> GetFixedProject(FixAllContext fixAllContext, Project project)
        {
            var solution = project.Solution;
            var newDocuments = project.Documents.ToDictionary(d => d.Id, d => GetFixedDocument(fixAllContext, d));
            await Task.WhenAll(newDocuments.Values).ConfigureAwait(false);
            foreach (var newDoc in newDocuments)
            {
                solution = solution.WithDocumentSyntaxRoot(newDoc.Key, newDoc.Value.Result);
            }
            return solution;
        }

        private async Task<SyntaxNode> GetFixedDocument(FixAllContext fixAllContext, Document document)
        {
            var diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false);
            var root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
            var nodes = diagnostics
                .Select(d => root.FindNode(d.Location.SourceSpan))
                .Where(n => !n.IsMissing);
            root = root.ReplaceNodes(nodes, (original, rewritten) => original.WithAdditionalAnnotations(annotation));
            var annotatedNodes = root.GetAnnotatedNodes(annotation);

            while (annotatedNodes.Any())
            {
                var node = annotatedNodes.First();
                root = calculateNewRoot(root, node);
                annotatedNodes = root.GetAnnotatedNodes(annotation);
            }
            return root;
        }
    }
}

