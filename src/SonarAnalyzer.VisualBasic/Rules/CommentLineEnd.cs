/*
 * SonarAnalyzer for .NET
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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("1min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    [Tags(Tag.Convention)]
    public class CommentLineEnd : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S139";
        internal const string Title = "Comments should not be located at the end of lines of code";
        internal const string Description =
            "This rule verifies that single-line comments are not located at the end of a line of code. The main idea " +
            "behind this rule is that in order to be really readable, trailing comments would have to be properly written " +
            "and formatted (correct alignment, no interference with the visual structure of the code, not too long to be " +
            "visible) but most often, automatic code formatters would not handle this correctly: the code would end up " +
            "less readable. Comments are far better placed on the previous empty line of code, where they will always be " +
            "visible and properly formatted.";
        internal const string MessageFormat = "Move this trailing comment on the previous empty line.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), false,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const string DefaultPattern = @"^'\s*\S+\s*$";

        [RuleParameter("legalCommentPattern", PropertyType.String,
            "Pattern for text of trailing comments that are allowed.", DefaultPattern)]
        public string LegalCommentPattern { get; set; } = DefaultPattern;

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    foreach (var token in c.Tree.GetRoot().DescendantTokens())
                    {
                        CheckTokenComments(token, c);
                    }
                });
        }

        private void CheckTokenComments(SyntaxToken token, SyntaxTreeAnalysisContext context)
        {
            var tokenLine = token.GetLocation().GetLineSpan().StartLinePosition.Line;

            var comments = token.TrailingTrivia
                .Where(tr => tr.IsKind(SyntaxKind.CommentTrivia));

            foreach (var comment in comments)
            {
                var location = comment.GetLocation();
                if (location.GetLineSpan().StartLinePosition.Line == tokenLine &&
                    !Regex.IsMatch(comment.ToString(), LegalCommentPattern))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, location));
                }
            }
        }
    }
}
