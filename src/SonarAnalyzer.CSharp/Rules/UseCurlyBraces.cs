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

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [SqaleConstantRemediation("2min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cert, Tag.Cwe, Tag.Misra, Tag.Pitfall)]
    public class UseCurlyBraces : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S121";
        internal const string Title = "Control structures should use curly braces";
        internal const string Description =
            "While not technically incorrect, the omission of curly braces can be misleading, and may " +
            "lead to the introduction of errors during maintenance.";
        internal const string MessageFormat = "Add curly braces around the nested statement(s) in this \"{0}\" block.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        private sealed class CheckedKind
        {
            public SyntaxKind Kind { get; set; }
            public string Value { get; set; }
            public Func<SyntaxNode, bool> Validator { get; set; }
            public Func<SyntaxNode, Location> IssueReportLocation { get; set; }
        }

        private static readonly ImmutableList<CheckedKind> CheckedKinds = ImmutableList.Create(
            new CheckedKind
            {
                Kind = SyntaxKind.IfStatement,
                Value = "if",
                Validator = node => ((IfStatementSyntax)node).Statement.IsKind(SyntaxKind.Block),
                IssueReportLocation = node => ((IfStatementSyntax)node).IfKeyword.GetLocation()
            },
            new CheckedKind
            {
                Kind = SyntaxKind.ElseClause,
                Value = "else",
                Validator =
                    node =>
                    {
                        var statement = ((ElseClauseSyntax)node).Statement;
                        return statement.IsKind(SyntaxKind.IfStatement) || statement.IsKind(SyntaxKind.Block);
                    },
                IssueReportLocation = node => ((ElseClauseSyntax)node).ElseKeyword.GetLocation()
            },
            new CheckedKind
            {
                Kind = SyntaxKind.ForStatement,
                Value = "for",
                Validator = node => ((ForStatementSyntax)node).Statement.IsKind(SyntaxKind.Block),
                IssueReportLocation = node => ((ForStatementSyntax)node).ForKeyword.GetLocation()
            },
            new CheckedKind
            {
                Kind = SyntaxKind.ForEachStatement,
                Value = "foreach",
                Validator = node => ((ForEachStatementSyntax)node).Statement.IsKind(SyntaxKind.Block),
                IssueReportLocation = node => ((ForEachStatementSyntax)node).ForEachKeyword.GetLocation()
            },
            new CheckedKind
            {
                Kind = SyntaxKind.DoStatement,
                Value = "do",
                Validator = node => ((DoStatementSyntax)node).Statement.IsKind(SyntaxKind.Block),
                IssueReportLocation = node => ((DoStatementSyntax)node).DoKeyword.GetLocation()
            },
            new CheckedKind
            {
                Kind = SyntaxKind.WhileStatement,
                Value = "while",
                Validator = node => ((WhileStatementSyntax)node).Statement.IsKind(SyntaxKind.Block),
                IssueReportLocation = node => ((WhileStatementSyntax)node).WhileKeyword.GetLocation()
            });

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var checkedKind = CheckedKinds.Single(e => c.Node.IsKind(e.Kind));

                    if (!checkedKind.Validator(c.Node))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, checkedKind.IssueReportLocation(c.Node), checkedKind.Value));
                    }
                },
                CheckedKinds.Select(e => e.Kind).ToArray());
        }
    }
}
