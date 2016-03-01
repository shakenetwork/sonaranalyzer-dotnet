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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Misra, Tag.Unused)]
    public class CommentedOutCode : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S125";
        internal const string Title = "Sections of code should not be \"commented out\"";
        internal const string Description =
            "Programmers should not comment out code as it bloats programs and reduces " +
            "readability. Unused code should be deleted and can be retrieved from source " +
            "control history if required.";
        internal const string MessageFormat = "Remove this commented out code.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    foreach (var token in c.Tree.GetRoot().DescendantTokens())
                    {
                        Action<IEnumerable<SyntaxTrivia>> check =
                            trivias =>
                            {
                                var shouldReport = true;
                                foreach (var trivia in trivias)
                                {
                                    if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                                    {
                                        CheckMultilineComment(c, trivia);
                                        shouldReport = true;
                                        continue;
                                    }

                                    if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && shouldReport)
                                    {
                                        var triviaContent = GetTriviaContent(trivia);
                                        if (!IsCode(triviaContent))
                                        {
                                            continue;
                                        }

                                        c.ReportDiagnostic(Diagnostic.Create(Rule, trivia.GetLocation()));
                                        shouldReport = false;
                                        continue;
                                    }
                                }
                            };

                        check(token.LeadingTrivia);
                        check(token.TrailingTrivia);
                    }
                });
        }

        private static void CheckMultilineComment(SyntaxTreeAnalysisContext context, SyntaxTrivia comment)
        {
            var triviaContent = GetTriviaContent(comment);
            var triviaLines = triviaContent.Split(MetricsBase.LineTerminators, StringSplitOptions.None);

            for (var triviaLineNumber = 0; triviaLineNumber < triviaLines.Length; triviaLineNumber++)
            {
                if (!IsCode(triviaLines[triviaLineNumber]))
                {
                    continue;
                }

                var triviaStartingLineNumber = comment.GetLocation().GetLineSpan().StartLinePosition.Line;
                var lineNumber = triviaStartingLineNumber + triviaLineNumber;
                var lineSpan = context.Tree.GetText().Lines[lineNumber].Span;
                var commentLineSpan = lineSpan.Intersection(comment.GetLocation().SourceSpan);

                var location = Location.Create(context.Tree, commentLineSpan ?? lineSpan);
                context.ReportDiagnostic(Diagnostic.Create(Rule, location));
                return;
            }
        }

        private static string GetTriviaContent(SyntaxTrivia trivia)
        {
            var triviaContent = trivia.ToString();
            if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                if (triviaContent.StartsWith("/*", StringComparison.Ordinal))
                {
                    triviaContent = triviaContent.Substring(2);
                }

                if (triviaContent.EndsWith("*/", StringComparison.Ordinal))
                {
                    triviaContent = triviaContent.Substring(0, triviaContent.Length-2);
                }
                return triviaContent;
            }

            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                if (triviaContent.StartsWith("//", StringComparison.Ordinal))
                {
                    triviaContent = triviaContent.Substring(2);
                }

                return triviaContent;
            }

            return string.Empty;
        }

        private static bool IsCode(string line)
        {
            var checkedLine = line
                .Replace(" ", string.Empty)
                .Replace("\t", string.Empty);

            var isPossiblyCode = EndsWithCode(checkedLine) ||
                    ContainsCodeParts(checkedLine) ||
                    ContainsMultipleLogicalOperators(checkedLine) ||
                    ContainsCodePartsWithRelationalOperator(checkedLine);

            return  isPossiblyCode &&
                !checkedLine.Contains("License");
        }

        private static bool ContainsMultipleLogicalOperators(string checkedLine)
        {
            var lineLength = checkedLine.Length;
            var lineLengthWithoutLogicalOperators = checkedLine
                .Replace("&&", string.Empty)
                .Replace("||", string.Empty)
                .Length;

            const int lengthOfOperator = 2;

            return lineLength - lineLengthWithoutLogicalOperators >= 3 * lengthOfOperator;
        }

        private static bool ContainsCodeParts(string checkedLine)
        {
            return CodeParts.Any(checkedLine.Contains);
        }

        private static bool ContainsCodePartsWithRelationalOperator(string checkedLine)
        {
            return CodePartsWithRelationalOperator.Any(codePart =>
            {
                var index = checkedLine.IndexOf(codePart, StringComparison.Ordinal);
                return index >= 0 && RelationalOperators.Any(op => checkedLine.IndexOf(op, index, StringComparison.Ordinal) >= 0);
            });
        }

        private static bool EndsWithCode(string checkedLine)
        {
            return CodeEndings.Any(ending => checkedLine.EndsWith(ending, StringComparison.Ordinal));
        }

        private static readonly string[] CodeEndings = { ";", "{", "}" };
        private static readonly string[] CodeParts = { "++", "catch(", "switch(", "try{", "else{" };
        private static readonly string[] CodePartsWithRelationalOperator = { "for(", "if(", "while(" };
        private static readonly string[] RelationalOperators = { "<", ">", "<=", ">=", "==", "!=" };
    }
}
