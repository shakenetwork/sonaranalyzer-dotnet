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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    [Tags("clumsy")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    public class BooleanLiteralUnnecessary : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1125";
        internal const string Title = "Literal boolean values should not be used in condition expressions";
        internal const string Description =
            "Remove literal boolean values from conditional expressions to improve readability. Anything that " +
            "can be tested for equality with a boolean value must itself be a boolean value, and boolean values " +
            "can be tested atomically.";
        internal const string MessageFormat = "Remove the literal \"{0}\" boolean value.";
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

        private static readonly ExpressionSyntax TrueExpression = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
        private static readonly ExpressionSyntax FalseExpression = SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var binaryExpression = (BinaryExpressionSyntax)c.Node;

                    CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, TrueExpression, c);
                    CheckForBooleanConstantOnLeftReportOnNormalLocation(binaryExpression, FalseExpression, c);

                    CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, TrueExpression, c);
                    CheckForBooleanConstantOnRightReportOnNormalLocation(binaryExpression, FalseExpression, c);
                },
                SyntaxKind.EqualsExpression,
                SyntaxKind.LogicalAndExpression);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var binaryExpression = (BinaryExpressionSyntax)c.Node;

                    CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, FalseExpression, c);
                    CheckForBooleanConstantOnLeftReportOnNormalLocation(binaryExpression, TrueExpression, c);

                    CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, FalseExpression, c);
                    CheckForBooleanConstantOnRightReportOnNormalLocation(binaryExpression, TrueExpression, c);
                },
                SyntaxKind.NotEqualsExpression,
                SyntaxKind.LogicalOrExpression);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var logicalNot = (PrefixUnaryExpressionSyntax)c.Node;

                    if (EquivalenceChecker.AreEquivalent(logicalNot.Operand, TrueExpression) ||
                        EquivalenceChecker.AreEquivalent(logicalNot.Operand, FalseExpression))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, logicalNot.Operand.GetLocation(), logicalNot.Operand.ToString()));
                    }
                },
                SyntaxKind.LogicalNotExpression);
        }

        private static void CheckForBooleanConstantOnLeftReportOnExtendedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, true, booleanContantExpression, true, c);
        }
        private static void CheckForBooleanConstantOnRightReportOnExtendedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, false, booleanContantExpression, true, c);
        }
        private static void CheckForBooleanConstantOnLeftReportOnNormalLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, true, booleanContantExpression, false, c);
        }
        private static void CheckForBooleanConstantOnRightReportOnNormalLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext c)
        {
            CheckForBooleanConstant(binaryExpression, false, booleanContantExpression, false, c);
        }

        private static void CheckForBooleanConstant(BinaryExpressionSyntax binaryExpression, bool leftSide,
            ExpressionSyntax booleanContantExpression, bool needsLocationCalculation, SyntaxNodeAnalysisContext c)
        {
            var expression = leftSide
                ? binaryExpression.Left
                : binaryExpression.Right;

            if (EquivalenceChecker.AreEquivalent(expression, booleanContantExpression))
            {
                var location = needsLocationCalculation
                    ? CalculateLocation(binaryExpression, leftSide)
                    : expression.GetLocation();

                c.ReportDiagnostic(Diagnostic.Create(Rule, location,
                    booleanContantExpression.ToString()));
            }
        }

        private static Location CalculateLocation(BinaryExpressionSyntax binaryExpression, bool leftSide)
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
