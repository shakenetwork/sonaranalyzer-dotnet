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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Misra, Tag.Unused)]
    public class CommentedOutCode : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S125";
        internal const string Title = "Sections of code should not be \"commented out\"";
        internal const string Description =
            "Programmers should not comment out code as it bloats programs and reduces " +
            "readability. Unused code should be deleted and can be retrieved from source " +
            "control history if required.";
        internal const string MessageFormat = "Remove this commented out code.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public static readonly string[] LineTerminators = { "\r\n", "\n", "\r" };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    foreach (var token in c.Tree.GetRoot().DescendantTokens())
                    {
                        Action<IEnumerable<SyntaxTrivia>> check =
                            trivias =>
                            {
                                var lastCommentedCodeLine = int.MinValue;

                                foreach (var trivia in trivias)
                                {
                                    if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) &&
                                        !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                                    {
                                        continue;
                                    }

                                    var triviaContent = GetTriviaContent(trivia);
                                    var triviaStartingLineNumber = trivia.GetLocation().GetLineSpan().StartLinePosition.Line;
                                    var triviaLines = triviaContent.Split(LineTerminators, StringSplitOptions.None);

                                    for (var triviaLineNumber = 0; triviaLineNumber < triviaLines.Length; triviaLineNumber++)
                                    {
                                        if (!IsCode(triviaLines[triviaLineNumber]))
                                        {
                                            continue;
                                        }

                                        var lineNumber = triviaStartingLineNumber + triviaLineNumber;
                                        var previousLastCommentedCodeLine = lastCommentedCodeLine;
                                        lastCommentedCodeLine = lineNumber;

                                        if (lineNumber == previousLastCommentedCodeLine + 1)
                                        {
                                            continue;
                                        }

                                        var lineSpan = c.Tree.GetText().Lines[lineNumber].Span;
                                        var commentLineSpan = lineSpan.Intersection(trivia.GetLocation().SourceSpan);

                                        var location = Location.Create(c.Tree, commentLineSpan ?? lineSpan);
                                        c.ReportDiagnostic(Diagnostic.Create(Rule, location));
                                        break;
                                    }
                                }
                            };

                        check(token.LeadingTrivia);
                        check(token.TrailingTrivia);
                    }
                });
        }

        private static string GetTriviaContent(SyntaxTrivia trivia)
        {
            var triviaContent = trivia.ToString();
            if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                if (triviaContent.StartsWith("/*", StringComparison.InvariantCulture))
                {
                    triviaContent = triviaContent.Substring(2);
                }

                if (triviaContent.EndsWith("*/", StringComparison.InvariantCulture))
                {
                    triviaContent = triviaContent.Substring(0, triviaContent.Length-2);
                }
                return triviaContent;
            }

            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                if (triviaContent.StartsWith("//", StringComparison.InvariantCulture))
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

            return
                (
                    EndsWithCode(checkedLine) ||
                    ContainsCodeParts(checkedLine) ||
                    ContainsMultipleLogicalOperators(checkedLine) ||
                    ContainsCodePartsWithRelationalOperator(checkedLine)
                ) &&
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
                var index = checkedLine.IndexOf(codePart, StringComparison.InvariantCulture);
                return index >= 0 && RelationalOperators.Any(op => checkedLine.IndexOf(op, index, StringComparison.InvariantCulture) >= 0);
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
