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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.Text;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [SqaleConstantRemediation("2min")]
    [Tags(Tag.Clumsy)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    public class BooleanLiteralUnnecessary : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1125";
        internal const string Title = "Boolean literals should not be redundant";
        internal const string Description =
            "Redundant Boolean literals should be removed from expressions to improve readability.";
        internal const string MessageFormat = "Remove the unnecessary Boolean literal(s).";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = false;
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

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckEquals(c),
                SyntaxKind.EqualsExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckLogicalAnd(c),
                SyntaxKind.LogicalAndExpression);

            context.RegisterSyntaxNodeAction(
                c => CheckNotEquals(c),
                SyntaxKind.NotEqualsExpression);

            context.RegisterSyntaxNodeAction(
                c => CheckLogicalOr(c),
                SyntaxKind.LogicalOrExpression);

            context.RegisterSyntaxNodeAction(
                c => CheckLogicalNot(c),
                SyntaxKind.LogicalNotExpression);

            context.RegisterSyntaxNodeAction(
                c => CheckConditional(c),
                SyntaxKind.ConditionalExpression);
        }

        private static void CheckConditional(SyntaxNodeAnalysisContext context)
        {
            var conditional = (ConditionalExpressionSyntax)context.Node;
            if (IsOnNullableBoolean(conditional, context.SemanticModel))
            {
                return;
            }

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

                context.ReportDiagnostic(Diagnostic.Create(Rule, side.GetLocation()));
                return;
            }

            var bothSideBool = whenTrueIsBooleanConstant && whenFalseIsBooleanConstant;
            var bothSideTrue = whenTrueIsTrue && whenFalseIsTrue;
            var bothSideFalse = whenTrueIsFalse && whenFalseIsFalse;

            if (bothSideBool && !bothSideFalse && !bothSideTrue)
            {
                var location = Location.Create(conditional.SyntaxTree,
                    new TextSpan(conditional.WhenTrue.SpanStart, conditional.WhenFalse.Span.End - conditional.WhenTrue.SpanStart));

                context.ReportDiagnostic(Diagnostic.Create(Rule, location));
                return;
            }
        }

        private static void CheckLogicalNot(SyntaxNodeAnalysisContext context)
        {
            var logicalNot = (PrefixUnaryExpressionSyntax)context.Node;

            if (EquivalenceChecker.AreEquivalent(logicalNot.Operand, TrueExpression) ||
                EquivalenceChecker.AreEquivalent(logicalNot.Operand, FalseExpression))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, logicalNot.Operand.GetLocation()));
            }
        }

        private static void CheckLogicalOr(SyntaxNodeAnalysisContext context)
        {
            var binaryExpression = (BinaryExpressionSyntax)context.Node;
            if (CheckForNullabilityAndBooleanConstantsReport(binaryExpression, false, context))
            {
                return;
            }

            CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, FalseExpression, context);
            CheckForBooleanConstantOnLeftReportOnInvertedLocation(binaryExpression, TrueExpression, context);

            CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, FalseExpression, context);
            CheckForBooleanConstantOnRightReportOnInvertedLocation(binaryExpression, TrueExpression, context);
        }

        private static void CheckNotEquals(SyntaxNodeAnalysisContext context)
        {
            var binaryExpression = (BinaryExpressionSyntax)context.Node;
            if (CheckForNullabilityAndBooleanConstantsReport(binaryExpression, false, context))
            {
                return;
            }

            CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, FalseExpression, context);
            CheckForBooleanConstantOnLeftReportOnNormalLocation(binaryExpression, TrueExpression, context);

            CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, FalseExpression, context);
            CheckForBooleanConstantOnRightReportOnNormalLocation(binaryExpression, TrueExpression, context);
        }

        private static void CheckLogicalAnd(SyntaxNodeAnalysisContext context)
        {
            var binaryExpression = (BinaryExpressionSyntax)context.Node;
            if (CheckForNullabilityAndBooleanConstantsReport(binaryExpression, true, context))
            {
                return;
            }

            CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, TrueExpression, context);
            CheckForBooleanConstantOnLeftReportOnInvertedLocation(binaryExpression, FalseExpression, context);

            CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, TrueExpression, context);
            CheckForBooleanConstantOnRightReportOnInvertedLocation(binaryExpression, FalseExpression, context);
        }

        private static void CheckEquals(SyntaxNodeAnalysisContext context)
        {
            var binaryExpression = (BinaryExpressionSyntax)context.Node;
            if (CheckForNullabilityAndBooleanConstantsReport(binaryExpression, true, context))
            {
                return;
            }

            CheckForBooleanConstantOnLeftReportOnExtendedLocation(binaryExpression, TrueExpression, context);
            CheckForBooleanConstantOnLeftReportOnNormalLocation(binaryExpression, FalseExpression, context);

            CheckForBooleanConstantOnRightReportOnExtendedLocation(binaryExpression, TrueExpression, context);
            CheckForBooleanConstantOnRightReportOnNormalLocation(binaryExpression, FalseExpression, context);
        }

        private static bool IsOnNullableBoolean(ConditionalExpressionSyntax conditionalExpression, SemanticModel semanticModel)
        {
            var typeLeft = semanticModel.GetTypeInfo(conditionalExpression.WhenTrue).Type;
            var typeRight = semanticModel.GetTypeInfo(conditionalExpression.WhenFalse).Type;
            return IsOnNullableBoolean(typeLeft, typeRight);
        }

        private static bool IsOnNullableBoolean(ITypeSymbol typeLeft, ITypeSymbol typeRight)
        {
            if (typeLeft == null || typeRight == null)
            {
                return false;
            }

            return IsNullableBoolean(typeLeft) || IsNullableBoolean(typeRight);
        }

        private static bool IsNullableBoolean(ITypeSymbol type)
        {
            if (!type.OriginalDefinition.Is(KnownType.System_Nullable_T))
            {
                return false;
            }

            var namedType = (INamedTypeSymbol)type;
            if(namedType.TypeArguments.Length != 1)
            {
                return false;
            }

            return namedType.TypeArguments[0].Is(KnownType.System_Boolean);
        }

        private static bool CheckForNullabilityAndBooleanConstantsReport(BinaryExpressionSyntax binaryExpression,
            bool reportOnTrue, SyntaxNodeAnalysisContext context)
        {
            var typeLeft = context.SemanticModel.GetTypeInfo(binaryExpression.Left).Type;
            var typeRight = context.SemanticModel.GetTypeInfo(binaryExpression.Right).Type;
            var shouldntReport = IsOnNullableBoolean(typeLeft, typeRight);
            if (shouldntReport)
            {
                return true;
            }

            var leftIsTrue = EquivalenceChecker.AreEquivalent(binaryExpression.Left, TrueExpression);
            var leftIsFalse = EquivalenceChecker.AreEquivalent(binaryExpression.Left, FalseExpression);
            var rightIsTrue = EquivalenceChecker.AreEquivalent(binaryExpression.Right, TrueExpression);
            var rightIsFalse = EquivalenceChecker.AreEquivalent(binaryExpression.Right, FalseExpression);

            var leftIsBoolean = leftIsTrue || leftIsFalse;
            var rightIsBoolean = rightIsTrue || rightIsFalse;

            if (leftIsBoolean && rightIsBoolean)
            {
                var bothAreSame = (leftIsTrue && rightIsTrue) || (leftIsFalse && rightIsFalse);
                var errorLocation = bothAreSame
                    ? CalculateExtendedLocation(binaryExpression, false)
                    : CalculateExtendedLocation(binaryExpression, reportOnTrue == leftIsTrue);

                context.ReportDiagnostic(Diagnostic.Create(Rule, errorLocation));
                return true;
            }
            return false;
        }

        private static void CheckForBooleanConstantOnLeftReportOnInvertedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext context)
        {
            CheckForBooleanConstant(binaryExpression, true, booleanContantExpression, ErrorLocation.Inverted, context);
        }

        private static void CheckForBooleanConstantOnRightReportOnInvertedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext context)
        {
            CheckForBooleanConstant(binaryExpression, false, booleanContantExpression, ErrorLocation.Inverted, context);
        }

        private static void CheckForBooleanConstantOnLeftReportOnExtendedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext context)
        {
            CheckForBooleanConstant(binaryExpression, true, booleanContantExpression, ErrorLocation.Extended, context);
        }
        private static void CheckForBooleanConstantOnRightReportOnExtendedLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext context)
        {
            CheckForBooleanConstant(binaryExpression, false, booleanContantExpression, ErrorLocation.Extended, context);
        }
        private static void CheckForBooleanConstantOnLeftReportOnNormalLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext context)
        {
            CheckForBooleanConstant(binaryExpression, true, booleanContantExpression, ErrorLocation.Normal, context);
        }
        private static void CheckForBooleanConstantOnRightReportOnNormalLocation(BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax booleanContantExpression, SyntaxNodeAnalysisContext context)
        {
            CheckForBooleanConstant(binaryExpression, false, booleanContantExpression, ErrorLocation.Normal, context);
        }

        private enum ErrorLocation
        {
            Normal,
            Extended,
            Inverted
        }

        private static void CheckForBooleanConstant(BinaryExpressionSyntax binaryExpression, bool leftSide,
            ExpressionSyntax booleanContantExpression, ErrorLocation errorLocation, SyntaxNodeAnalysisContext context)
        {
            var expression = leftSide
                ? binaryExpression.Left
                : binaryExpression.Right;

            if (!EquivalenceChecker.AreEquivalent(expression, booleanContantExpression))
            {
                return;
            }

            Location location;
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
                default:
                    location = null;
                    break;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location));            
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
