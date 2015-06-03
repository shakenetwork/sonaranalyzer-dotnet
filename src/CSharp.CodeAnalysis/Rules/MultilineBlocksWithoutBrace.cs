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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("bug")]
    public class MultilineBlocksWithoutBrace : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2681";
        internal const string Title = "Multiline blocks should be enclosed in curly braces";
        internal const string Description =
            "Curly braces can be omitted from a one-line block, such as with an \"if\" statement " +
            "or \"for\" loop, but doing so can be misleading and induce bugs. This rule raises an " +
            "issue when the indention of the lines after a one-line block indicates an intent to " +
            "include those lines in the block, but the omission of curly braces means the lines " +
            "will be unconditionally executed once.";
        internal const string MessageFormat = 
            "Only the first line of this n-line block will be executed {0}. The rest will execute {1}.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical; 
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2681",
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c => CheckLoop(c, ((WhileStatementSyntax) c.Node).Statement),
                SyntaxKind.WhileStatement);
            context.RegisterSyntaxNodeAction(
                c => CheckLoop(c, ((ForStatementSyntax) c.Node).Statement),
                SyntaxKind.ForStatement);
            context.RegisterSyntaxNodeAction(
                c => CheckLoop(c, ((ForEachStatementSyntax) c.Node).Statement),
                SyntaxKind.ForEachStatement);
            context.RegisterSyntaxNodeAction(
                c => CheckIf(c, (IfStatementSyntax) c.Node),
                SyntaxKind.IfStatement);
        }

        private static void CheckLoop(SyntaxNodeAnalysisContext c, StatementSyntax statement)
        {
            CheckStatement(c, statement, "in a loop", "only once");
        }

        private static void CheckIf(SyntaxNodeAnalysisContext c, IfStatementSyntax ifStatement)
        {
            CheckStatement(c, ifStatement.Else == null ? ifStatement.Statement : ifStatement.Else.Statement,
                "conditionally", "unconditionally");
        }

        private static void CheckStatement(SyntaxNodeAnalysisContext c, StatementSyntax statement, 
            string executed, string execute)
        {
            if (statement is BlockSyntax)
            {
                return;
            }

            var nextStatement = c.Node.GetLastToken().GetNextToken().Parent;
            if (nextStatement == null)
            {
                return;
            }

            var charPositionWithinLine1 = statement.GetLocation().GetLineSpan().StartLinePosition.Character;
            var charPositionWithinLine2 = nextStatement.GetLocation().GetLineSpan().StartLinePosition.Character;

            if (charPositionWithinLine1 == charPositionWithinLine2)
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, nextStatement.GetLocation(), executed, execute));
            }
        }
    }
}
