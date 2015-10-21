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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SonarLint.Common
{
    public abstract class MetricsBase
    {
        protected readonly SyntaxTree tree;

        protected MetricsBase(SyntaxTree tree)
        {
            this.tree = tree;
        }

        #region LinesOfCode

        public int GetLineCount()
        {
            return tree
                .GetLineSpan(TextSpan.FromBounds(tree.Length, tree.Length))
                .EndLinePosition
                .Line + 1;
        }

        public IImmutableSet<int> GetLinesOfCode()
        {
            return tree.GetRoot()
                .DescendantTokens()
                .Where(token => !IsEndOfFile(token))
                .SelectMany(
                    t =>
                    {
                        var start = t.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        var end = t.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
                        return Enumerable.Range(start, end - start + 1);
                    })
                .ToImmutableHashSet();
        }

        protected abstract Func<SyntaxToken, bool> IsEndOfFile { get; }

        #endregion

        #region Comments

        public FileComments GetComments(bool ignoreHeaderComments)
        {
            var noSonar = ImmutableHashSet.CreateBuilder<int>();
            var nonBlank = ImmutableHashSet.CreateBuilder<int>();

            var trivias = tree.GetRoot().DescendantTrivia();

            foreach (var trivia in trivias)
            {
                if (!IsCommentTrivia(trivia) ||
                    ignoreHeaderComments && IsNoneToken(trivia.Token.GetPreviousToken()))
                {
                    continue;
                }

                var lineNumber = tree
                    .GetLineSpan(trivia.FullSpan)
                    .StartLinePosition
                    .Line + 1;

                var triviaLines = trivia
                    .ToFullString()
                    .Split(Rules.CommentedOutCode.LineTerminators, StringSplitOptions.None);

                foreach (var line in triviaLines)
                {
                    if (line.Contains("NOSONAR"))
                    {
                        nonBlank.Remove(lineNumber);
                        noSonar.Add(lineNumber);
                    }
                    else
                    {
                        if (HasValidCommentContent(line) &&
                                !noSonar.Contains(lineNumber))
                        {
                            nonBlank.Add(lineNumber);
                        }
                    }

                    lineNumber++;
                }
            }

            return new FileComments(noSonar.ToImmutableHashSet(), nonBlank.ToImmutableHashSet());
        }

        protected abstract Func<string, bool> HasValidCommentContent { get; }
        protected abstract Func<SyntaxToken, bool> IsNoneToken { get; }
        protected abstract Func<SyntaxTrivia, bool> IsCommentTrivia { get; }

        #endregion

        #region Classes, Accessors, Functions, Statements

        public int GetClassCount()
        {
            return tree.GetRoot()
                .DescendantNodes()
                .Count(IsClass);
        }
        protected abstract Func<SyntaxNode, bool> IsClass { get; }

        public int GetAccessorCount()
        {
            return tree.GetRoot()
                .DescendantNodes()
                .Count(IsAccessor);
        }
        protected abstract Func<SyntaxNode, bool> IsAccessor { get; }

        public int GetStatementCount()
        {
            return tree.GetRoot()
                .DescendantNodes()
                .Count(IsStatement);
        }
        protected abstract Func<SyntaxNode, bool> IsStatement { get; }

        public int GetFunctionCount()
        {
            return GetFunctionNodes().Count();
        }

        public IEnumerable<SyntaxNode> GetFunctionNodes()
        {
            return tree.GetRoot()
                .DescendantNodes()
                .Where(IsFunctionWithBody);
        }

        protected abstract Func<SyntaxNode, bool> IsFunction { get; }
        protected abstract Func<SyntaxNode, bool> IsFunctionWithBody { get; }

        #endregion

        #region PublicApi

        public int GetPublicApiCount()
        {
            return GetPublicApiNodes().Count();
        }

        public int GetPublicUndocumentedApiCount()
        {
            return GetPublicApiNodes()
                .Count(n =>
                    !n.GetLeadingTrivia().Any(IsCommentTrivia));
        }

        protected abstract IEnumerable<SyntaxNode> GetPublicApiNodes();

        #endregion

        #region Complexity

        public int GetComplexity()
        {
            return GetComplexity(tree.GetRoot());
        }

        public abstract int GetComplexity(SyntaxNode node);

        public Distribution GetFunctionComplexityDistribution()
        {
            var distribution = new Distribution(1, 2, 4, 6, 8, 10, 12);
            foreach (var node in GetFunctionNodes())
            {
                distribution.Add(GetComplexity(node));
            }
            return distribution;
        }

        #endregion
    }
}
