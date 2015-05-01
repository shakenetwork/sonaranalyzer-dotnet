/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
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
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("misra", "unused")]
    public class CommentedOutCode : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S125";
        internal const string Description = "Sections of code should not be \"commented out\"";
        internal const string MessageFormat = "Remove this commented out code.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3ACommentedCode");
        
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeAction(
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

                                    var triviaContent = trivia.ToString().Substring(2);
                                    if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                                    {
                                        triviaContent = triviaContent.Substring(0, triviaContent.Length - 2);
                                    }

                                    var triviaStartingLineNumber = trivia.GetLocation().GetLineSpan().StartLinePosition.Line;
                                    // TODO Do not duplicate line terminators here
                                    var triviaLines = triviaContent.Split(new [] { "\r\n", "\n" }, StringSplitOptions.None);

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

        private static bool IsCode(string line)
        {
            var checkedLine = line.Replace(" ", "").Replace("\t", "");

            return
                (CodeEndings.Any(ending => checkedLine.EndsWith(ending, StringComparison.Ordinal)) ||
                 CodeParts.Any(part => checkedLine.Contains(part)) ||
                 (checkedLine.Length - checkedLine.Replace("&&", "").Replace("||", "").Length)/2 >= 3) &&
                !checkedLine.Contains("License");
        }

        private static readonly string[] CodeEndings = { ";", "{", "}" };
        private static readonly string[] CodeParts = { "++", "for(", "if(", "while(", "catch(", "switch(", "try{", "else{" };
    }
}
