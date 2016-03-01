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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.Text;

namespace SonarLint.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class OnErrorStatement : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2359";
        internal const string Title = "\"On Error\" statements should not be used";
        internal const string Description =
            "Prefer the use of \"Try ... Catch\" blocks instead of \"On Error\" statements. Structured exception handling is " +
            "more powerful because it allows you to nest error handlers inside other error handlers within the same procedure.";
        internal const string MessageFormat = "Remove this use of \"OnError\".";
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
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var node = (OnErrorGoToStatementSyntax)c.Node;
                    c.ReportDiagnostic(Diagnostic.Create(Rule,
                        Location.Create(node.SyntaxTree, TextSpan.FromBounds(node.OnKeyword.SpanStart, node.ErrorKeyword.Span.End))));
                },
                SyntaxKind.OnErrorGoToLabelStatement,
                SyntaxKind.OnErrorGoToZeroStatement,
                SyntaxKind.OnErrorGoToMinusOneStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var node = (OnErrorResumeNextStatementSyntax)c.Node;
                    c.ReportDiagnostic(Diagnostic.Create(Rule,
                        Location.Create(node.SyntaxTree, TextSpan.FromBounds(node.OnKeyword.SpanStart, node.ErrorKeyword.Span.End))));
                },
                SyntaxKind.OnErrorResumeNextStatement);
        }
    }
}
