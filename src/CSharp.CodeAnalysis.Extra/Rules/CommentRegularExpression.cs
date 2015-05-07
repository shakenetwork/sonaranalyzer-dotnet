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

using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    public class CommentRegularExpressionRule
    {
        public DiagnosticDescriptor Descriptor;
        public string RegularExpression;
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [NoSqaleRemediation]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault, true)]
    public class CommentRegularExpression : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "S124";
        internal const string Description = "Regular expression on comment";
        internal const string MessageFormat = "The regular expression matches this comment";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        public static DiagnosticDescriptor CreateDiagnosticDescriptor(string diagnosticId, string messageFormat)
        {
            return new DiagnosticDescriptor(diagnosticId, Description, messageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS124");
        }

        public ImmutableArray<CommentRegularExpressionRule> Rules;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return Rules.Select(r => r.Descriptor).ToImmutableArray(); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeAction(
                c =>
                {
                    var comments = from t in c.Tree.GetCompilationUnitRoot().DescendantTrivia()
                                   where IsComment(t)
                                   select t;

                    foreach (var comment in comments)
                    {
                        var text = comment.ToString();
                        foreach (var rule in Rules.Where(rule => Regex.IsMatch(text, rule.RegularExpression)))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(rule.Descriptor, comment.GetLocation()));
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
