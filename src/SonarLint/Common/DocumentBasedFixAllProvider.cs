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
using System.Collections.Generic;

namespace SonarLint.Common
{
    internal sealed class DocumentBasedFixAllProvider<T> : FixAllProvider
    {
        private readonly string title;
        private readonly Func<SyntaxNode, SyntaxNode, Diagnostic, SyntaxNode> calculateNewRoot;
        internal static readonly string AnnotationKind = typeof(T).Name;

        public DocumentBasedFixAllProvider(string title, Func<SyntaxNode, SyntaxNode, Diagnostic, SyntaxNode> calculateNewRoot)
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

            var nodeDiagnosticPairs = diagnostics
                .Select(d => new KeyValuePair<SyntaxNode, Diagnostic>(root.FindNode(d.Location.SourceSpan, getInnermostNodeForTie: true), d))
                .Where(n => !n.Key.IsMissing)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var diagnosticAnnotationPairs = new BidirectionalDictionary<Diagnostic, SyntaxAnnotation>();
            CreateAnnotationForDiagnostics(diagnostics, diagnosticAnnotationPairs);
            root = GetRootWithAnnotatedNodes(root, nodeDiagnosticPairs, diagnosticAnnotationPairs);

            var annotatedNodes = root.GetAnnotatedNodes(AnnotationKind);
            while (annotatedNodes.Any())
            {
                var node = annotatedNodes.First();

                var annotation = node.GetAnnotations(AnnotationKind).First();
                var diagnostic = diagnosticAnnotationPairs.GetByB(annotation);
                root = calculateNewRoot(root, node, diagnostic);

                root = RemovePossiblyLeftAnnotation(root, annotation);
                annotatedNodes = root.GetAnnotatedNodes(AnnotationKind);
            }
            return root;
        }

        private static SyntaxNode RemovePossiblyLeftAnnotation(SyntaxNode root, SyntaxAnnotation annotation)
        {
            var newNode = root.GetAnnotatedNodes(annotation).FirstOrDefault();
            return newNode == null
                ? root
                : root.ReplaceNode(
                    newNode,
                    newNode.WithoutAnnotations(annotation));
        }

        private static SyntaxNode GetRootWithAnnotatedNodes(SyntaxNode root,
            Dictionary<SyntaxNode, Diagnostic> nodeDiagnosticPairs,
            BidirectionalDictionary<Diagnostic, SyntaxAnnotation> diagnosticAnnotationPairs)
        {
            return root.ReplaceNodes(
                nodeDiagnosticPairs.Keys,
                (original, rewritten) =>
                {
                    var annotation = diagnosticAnnotationPairs.GetByA(nodeDiagnosticPairs[original]);
                    return rewritten.WithAdditionalAnnotations(annotation);
                });
        }

        private static void CreateAnnotationForDiagnostics(System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics,
            BidirectionalDictionary<Diagnostic, SyntaxAnnotation> diagnosticAnnotationPairs)
        {
            foreach (var diagnostic in diagnostics)
            {
                diagnosticAnnotationPairs.Add(diagnostic, new SyntaxAnnotation(AnnotationKind));
            }
        }

        private class BidirectionalDictionary<TA, TB>
        {
            private readonly IDictionary<TA, TB> aToB = new Dictionary<TA, TB>();
            private readonly IDictionary<TB, TA> bToA = new Dictionary<TB, TA>();

            public void Add(TA a, TB b)
            {
                if (aToB.ContainsKey(a) || bToA.ContainsKey(b))
                {
                    throw new ArgumentException("An element with the same key already exists in the BidirectionalDictionary");
                }

                aToB.Add(a, b);
                bToA.Add(b, a);
            }

            public TB GetByA(TA a)
            {
                return aToB[a];
            }

            public TA GetByB(TB b)
            {
                return bToA[b];
            }
        }
    }
}

