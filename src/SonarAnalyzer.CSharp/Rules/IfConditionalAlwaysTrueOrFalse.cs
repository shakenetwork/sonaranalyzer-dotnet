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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using Microsoft.CodeAnalysis.Text;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [SqaleConstantRemediation("2min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Cwe, Tag.Misra, Tag.Security)]
    public class IfConditionalAlwaysTrueOrFalse : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1145";
        internal const string Title = "Useless \"if(true) {...}\" and \"if(false){...}\" blocks should be removed";
        internal const string Description =
            "\"if\" statements with conditions that are always false have the effect of making " +
            "blocks of code non-functional. This can be useful during debugging, but should not " +
            "be checked in. \"if\" statements with conditions that are always true are completely " +
            "redundant, and make the code less readable. In either case, unconditional \"if\" " +
            "statements should be removed.";
        internal const string MessageFormat = "Remove this useless {0}.";
        private const string ifStatementLiteral = "\"if\" statement";
        private const string elseClauseLiteral = "\"else\" clause";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var ifNode = (IfStatementSyntax)c.Node;

                    var isTrue = ifNode.Condition.IsKind(SyntaxKind.TrueLiteralExpression);
                    var isFalse = ifNode.Condition.IsKind(SyntaxKind.FalseLiteralExpression);

                    if (!isTrue && !isFalse)
                    {
                        return;
                    }

                    if (isTrue)
                    {
                        ReportIfTrue(ifNode, c);
                    }
                    else
                    {
                        ReportIfFalse(ifNode, c);
                    }
                },
                SyntaxKind.IfStatement);
        }

        private static void ReportIfFalse(IfStatementSyntax ifStatement, SyntaxNodeAnalysisContext context)
        {
            var location = ifStatement.Else == null
                ? ifStatement.GetLocation()
                : Location.Create(
                    ifStatement.SyntaxTree,
                    new TextSpan(ifStatement.IfKeyword.SpanStart, ifStatement.Else.ElseKeyword.Span.End - ifStatement.IfKeyword.SpanStart));

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, ifStatementLiteral));
        }

        private static void ReportIfTrue(IfStatementSyntax ifStatement, SyntaxNodeAnalysisContext context)
        {
            var location = Location.Create(
                ifStatement.SyntaxTree,
                new TextSpan(ifStatement.IfKeyword.SpanStart, ifStatement.CloseParenToken.Span.End - ifStatement.IfKeyword.SpanStart));

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, ifStatementLiteral));

            if (ifStatement.Else != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, ifStatement.Else.GetLocation(), elseClauseLiteral));
            }
        }
    }
}
