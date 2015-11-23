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
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.Text;

namespace SonarLint.Rules.CSharp
{
    public abstract class CommentRegularExpressionBase : DiagnosticAnalyzer
    {
        protected abstract string RegularExpression { get; }
        protected abstract DiagnosticDescriptor Rule { get; }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    var comments = c.Tree.GetCompilationUnitRoot().DescendantTrivia()
                        .Where(trivia => IsComment(trivia));

                    foreach (var comment in comments)
                    {
                        var text = comment.ToString();

                        foreach (var match in Regex.Matches(text, RegularExpression).OfType<Match>())
                        {
                            if (match.Groups.Count < 2)
                            {
                                continue;
                            }

                            var group = match.Groups[1];
                            var startLocation = comment.SpanStart + group.Index;
                            var location = Location.Create(
                                c.Tree,
                                TextSpan.FromBounds(startLocation, startLocation + group.Length));

                            c.ReportDiagnostic(Diagnostic.Create(Rule, location));
                        }
                    }
                });
        }

        private static bool IsComment(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
        }
    }
}
