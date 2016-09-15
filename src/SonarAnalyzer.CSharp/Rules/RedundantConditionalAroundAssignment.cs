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
using SonarAnalyzer.Helpers.CSharp;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Confusing)]
    public class RedundantConditionalAroundAssignment : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3440";
        internal const string Title = "Variables should not be checked against the values they're about to be assigned";
        internal const string Description =
            "There's no point in checking a variable against the value you're about to assign it. Save the cycles and lines of code, " +
            "and simply perform the assignment.";
        internal const string MessageFormat = "Remove this useless conditional.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
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
                    var ifStatement = (IfStatementSyntax)c.Node;
                    if (ifStatement.Else != null ||
                        ifStatement.Parent is ElseClauseSyntax)
                    {
                        return;
                    }

                    AssignmentExpressionSyntax assignment;
                    BinaryExpressionSyntax condition;
                    if (!TryGetNotEqualsCondition(ifStatement, out condition) ||
                        !TryGetSingleAssignment(ifStatement, out assignment))
                    {
                        return;
                    }

                    var expression1Condition = condition.Left?.RemoveParentheses();
                    var expression2Condition = condition.Right?.RemoveParentheses();
                    var expression1Assignment = assignment.Left?.RemoveParentheses();
                    var expression2Assignment = assignment.Right?.RemoveParentheses();

                    if (AreMatchingExpressions(expression1Condition, expression2Condition, expression2Assignment, expression1Assignment) ||
                        AreMatchingExpressions(expression1Condition, expression2Condition, expression1Assignment, expression2Assignment))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, condition.GetLocation()));
                    }
                },
                SyntaxKind.IfStatement);
        }

        private static bool TryGetNotEqualsCondition(IfStatementSyntax ifStatement, out BinaryExpressionSyntax condition)
        {
            condition = ifStatement.Condition?.RemoveParentheses() as BinaryExpressionSyntax;
            return condition != null &&
                condition.IsKind(SyntaxKind.NotEqualsExpression);
        }

        private static bool TryGetSingleAssignment(IfStatementSyntax ifStatement, out AssignmentExpressionSyntax assignment)
        {
            assignment = null;

            var statement = ifStatement.Statement;
            var block = statement as BlockSyntax;
            if (block != null &&
                block.Statements.Count == 1)
            {
                statement = block.Statements.First();
            }
            else
            {
                return false;
            }

            assignment = (statement as ExpressionStatementSyntax)?.Expression as AssignmentExpressionSyntax;
            return assignment != null &&
                assignment.IsKind(SyntaxKind.SimpleAssignmentExpression);
        }

        private static bool AreMatchingExpressions(ExpressionSyntax condition1, ExpressionSyntax condition2,
            ExpressionSyntax assignment1, ExpressionSyntax assignment2)
        {
            return EquivalenceChecker.AreEquivalent(condition1, assignment1) &&
                EquivalenceChecker.AreEquivalent(condition2, assignment2);
        }
    }
}
