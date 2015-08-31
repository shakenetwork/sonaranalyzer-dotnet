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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.Text;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [SqaleConstantRemediation("2min")]
    [Tags(Tag.Clumsy)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    public class BooleanLiteralUnnecessary : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1125";
        internal const string Title = "Boolean literals should not be redundant";
        internal const string Description =
            "Redundant Boolean literals should be removed from expressions to improve readability.";
        internal const string MessageFormat = "Remove the unnecessary Boolean literal(s).";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        internal static readonly ExpressionSyntax TrueExpression = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
        internal static readonly ExpressionSyntax FalseExpression = SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binaryExpression = (BinaryExpressionSyntax)c.Node;

                    if (CheckForBooleanConstantsReport(binaryExpression, true, c))
                    {
                        return;
                    }

                    CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, TrueExpression, c);
                    CheckForBooleanConstantOnLeftReportOnNormalLocation(binaryExpression, FalseExpression, c);

                    CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, TrueExpression, c);
                    CheckForBooleanConstantOnRightReportOnNormalLocation(binaryExpression, FalseExpression, c);
                },
                SyntaxKind.EqualsExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binaryExpression = (BinaryExpressionSyntax)c.Node;

                    if (CheckForBooleanConstantsReport(binaryExpression, true, c))
                    {
                        return;
                    }

                    CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, TrueExpression, c);
                    CheckForBooleanConstantOnLeftReportOnInvertedLocation(binaryExpression, FalseExpression, c);

                    CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, TrueExpression, c);
                    CheckForBooleanConstantOnRightReportOnInvertedLocation(binaryExpression, FalseExpression, c);
                },
                SyntaxKind.LogicalAndExpression);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var binaryExpression = (BinaryExpressionSyntax)c.Node;

                    if (CheckForBooleanConstantsReport(binaryExpression, false, c))
                    {
                        return;
                    }

                    CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, FalseExpression, c);
                    CheckForBooleanConstantOnLeftReportOnNormalLocation(binaryExpression, TrueExpression, c);

                    CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, FalseExpression, c);
                    CheckForBooleanConstantOnRightReportOnNormalLocation(binaryExpression, TrueExpression, c);
                },
                SyntaxKind.NotEqualsExpression);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var binaryExpression = (BinaryExpressionSyntax)c.Node;

                    if (CheckForBooleanConstantsReport(binaryExpression, false, c))
                    {
                        return;
                    }

                    CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, FalseExpression, c);
                    CheckForBooleanConstantOnLeftReportOnInvertedLocation(binaryExpression, TrueExpression, c);

                    CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, FalseExpression, c);
                    CheckForBooleanConstantOnRightReportOnInvertedLocation(binaryExpression, TrueExpression, c);
                },
                SyntaxKind.LogicalOrExpression);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var logicalNot = (PrefixUnaryExpressionSyntax)c.Node;

                    if (EquivalenceChecker.AreEquivalent(logicalNot.Operand, TrueExpression) ||
                        EquivalenceChecker.AreEquivalent(logicalNot.Operand, FalseExpression))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, logicalNot.Operand.GetLocation()));
                    }
                },
                SyntaxKind.LogicalNotExpression);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var conditional = (ConditionalExpressionSyntax)c.Node;

                    var whenTrueIsTrue = EquivalenceChecker.AreEquivalent(conditional.WhenTrue, TrueExpression);
                    var whenTrueIsFalse = EquivalenceChecker.AreEquivalent(conditional.WhenTrue, FalseExpression);
                    var whenFalseIsTrue = EquivalenceChecker.AreEquivalent(conditional.WhenFalse, TrueExpression);
                    var whenFalseIsFalse = EquivalenceChecker.AreEquivalent(conditional.WhenFalse, FalseExpression);

                    var whenTrueIsBooleanConstant = whenTrueIsTrue || whenTrueIsFalse;
                    var whenFalseIsBooleanConstant = whenFalseIsTrue || whenFalseIsFalse;

                    if (whenTrueIsBooleanConstant ^ whenFalseIsBooleanConstant)
                    {
                        var side = whenTrueIsBooleanConstant
                            ? conditional.WhenTrue
                            : conditional.WhenFalse;

                        c.ReportDiagnostic(Diagnostic.Create(Rule, side.GetLocation()));
                        return;
                    }

                    var bothSideBool = whenTrueIsBooleanConstant && whenFalseIsBooleanConstant;
                    var bothSideTrue = whenTrueIsTrue && whenFalseIsTrue;
                    var bothSideFalse = whenTrueIsFalse && whenFalseIsFalse;

                    if (bothSideBool && !bothSideFalse && !bothSideTrue)
                    {
                        var location = Location.Create(conditional.SyntaxTree,
                            new TextSpan(conditional.WhenTrue.SpanStart, conditional.WhenFalse.Span.End - conditional.WhenTrue.SpanStart));

                        c.ReportDiagnostic(Diagnostic.Create(Rule, location));
                        return;
                    }
                },
                SyntaxKind.ConditionalExpression);
        }

        private bool CheckForBooleanConstantsReport(BinaryExpressionSyntax binaryExpression, bool reportOnTrue, SyntaxNodeAnalysisContext c)
        {
            var leftIsTrue = EquivalenceChecker.AreEquivalent(binaryExpression.Left, TrueExpression);
            var leftIsFalse = EquivalenceChecker.AreEquivalent(binaryExpression.Left, FalseExpression);
            var rightIsTrue = EquivalenceChecker.AreEquivalent(binaryExpression.Right, TrueExpression);
            var rightIsFalse = EquivalenceChecker.AreEquivalent(binaryExpression.Right, FalseExpression);

            var leftIsBoolean = leftIsTrue || leftIsFalse;
            var rightIsBoolean = rightIsTrue || rightIsFalse;

            if (leftIsBoolean && rightIsBoolean)
            {
                var errorLocation = (leftIsTrue && rightIsTrue) || (leftIsFalse && rightIsFalse)
                    ? CalculateExtendedLocation(binaryExpression, false)
                    : CalculateExtendedLocation(binaryExpression, reportOnTrue == leftIsTrue);

                c.ReportDiagnostic(Diagnostic.Create(Rule, errorLocation));
                return true;
            }
            return false;
        }

        private void CheckForBooleanConstantOnLeftReportOnInvertedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, true, booleanContantExpression, ErrorLocation.Inverted, c);
        }

        private void CheckForBooleanConstantOnRightReportOnInvertedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, false, booleanContantExpression, ErrorLocation.Inverted, c);
        }

        private static void CheckForBooleanConstantOnLeftReportOnExtendedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, true, booleanContantExpression, ErrorLocation.Extended, c);
        }
        private static void CheckForBooleanConstantOnRightReportOnExtendedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, false, booleanContantExpression, ErrorLocation.Extended, c);
        }
        private static void CheckForBooleanConstantOnLeftReportOnNormalLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, true, booleanContantExpression, ErrorLocation.Normal, c);
        }
        private static void CheckForBooleanConstantOnRightReportOnNormalLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, false, booleanContantExpression, ErrorLocation.Normal, c);
        }

        private enum ErrorLocation
        {
            Normal,
            Extended,
            Inverted
        }

        private static void CheckForBooleanConstant(BinaryExpressionSyntax binaryExpression, bool leftSide,
            ExpressionSyntax booleanContantExpression, ErrorLocation errorLocation, SyntaxNodeAnalysisContext c)
        {
            var expression = leftSide
                ? binaryExpression.Left
                : binaryExpression.Right;

            if (EquivalenceChecker.AreEquivalent(expression, booleanContantExpression))
            {
                Location location = null;
                switch (errorLocation)
                {
                    case ErrorLocation.Normal:
                        location = expression.GetLocation();
                        break;
                    case ErrorLocation.Extended:
                        location = CalculateExtendedLocation(binaryExpression, leftSide);
                        break;
                    case ErrorLocation.Inverted:
                        location = CalculateExtendedLocation(binaryExpression, !leftSide);
                        break;
                }

                c.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        }

        private static Location CalculateExtendedLocation(BinaryExpressionSyntax binaryExpression, bool leftSide)
        {
            return leftSide
                ? Location.Create(binaryExpression.SyntaxTree,
                        new TextSpan(binaryExpression.SpanStart,
                            binaryExpression.OperatorToken.Span.End - binaryExpression.SpanStart))
                : Location.Create(binaryExpression.SyntaxTree,
                        new TextSpan(binaryExpression.OperatorToken.SpanStart,
                            binaryExpression.Span.End - binaryExpression.OperatorToken.SpanStart));
        }
    }
}
