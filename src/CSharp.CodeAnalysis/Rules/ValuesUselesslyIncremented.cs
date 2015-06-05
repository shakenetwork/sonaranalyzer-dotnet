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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug")]
    public class ValuesUselesslyIncremented : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2123";
        internal const string Description = "Values should not be uselessly incremented";
        internal const string MessageFormat = "Remove this {0} or correct the code not to waste it.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2123");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var increment = (PostfixUnaryExpressionSyntax)c.Node;

                    var operatorText = increment.OperatorToken.IsKind(SyntaxKind.PlusPlusToken)
                        ? "increment"
                        : "decrement";

                    if (increment.Parent is ReturnStatementSyntax)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, increment.GetLocation(), operatorText));
                        return;
                    }

                    var assignment = increment.Parent as AssignmentExpressionSyntax;
                    if (assignment != null &&
                        assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                        assignment.Right == increment &&
                        EquivalenceChecker.AreEquivalent(assignment.Left, increment.Operand))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, increment.GetLocation(), operatorText));
                    }
                },
                SyntaxKind.PostIncrementExpression, SyntaxKind.PostDecrementExpression);
        }
    }
}
