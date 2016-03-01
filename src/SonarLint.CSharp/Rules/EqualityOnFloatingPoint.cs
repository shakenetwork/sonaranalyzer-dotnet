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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Misra)]
    public class EqualityOnFloatingPoint : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1244";
        internal const string Title = "Floating point numbers should not be tested for equality";
        internal const string Description =
            "Floating point math is imprecise because of the challenges of storing such values " +
            "in a binary representation.Even worse, floating point math is not associative; " +
            "push a \"float\" or a \"double\" through a series of simple mathematical " +
            "operations and the answer will be different based on the order of those operation " +
            "because of the rounding that takes place at each step.";
        internal const string MessageFormat = "Do not check {0} with exact values, use a range instead.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly ISet<string> EqualityOperators = ImmutableHashSet.Create(new[]
        {
            "op_Equality",
            "op_Inequality"
        });

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckEquality(c),
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckLogicalExpression(c),
                SyntaxKind.LogicalAndExpression,
                SyntaxKind.LogicalOrExpression);
        }

        private static void CheckLogicalExpression(SyntaxNodeAnalysisContext context)
        {
            var binaryExpression = (BinaryExpressionSyntax)context.Node;
            var left = TryGetBinaryExpression(binaryExpression.Left);
            var right = TryGetBinaryExpression(binaryExpression.Right);

            if (right == null || left == null)
            {
                return;
            }

            var eqRight = EquivalenceChecker.AreEquivalent(right.Right, left.Right);
            var eqLeft = EquivalenceChecker.AreEquivalent(right.Left, left.Left);
            if (!eqRight || !eqLeft)
            {
                return;
            }

            if (IsIndirectEquality(binaryExpression, right, left, context) ||
                IsIndirectInequality(binaryExpression, right, left, context))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, binaryExpression.GetLocation(), "inequality"));
            }
        }

        private static void CheckEquality(SyntaxNodeAnalysisContext context)
        {
            var equals = (BinaryExpressionSyntax)context.Node;
            var equalitySymbol = context.SemanticModel.GetSymbolInfo(equals).Symbol as IMethodSymbol;

            if (equalitySymbol != null &&
                equalitySymbol.ContainingType != null &&
                equalitySymbol.ContainingType.IsAny(KnownType.FloatingPointNumbers) &&
                EqualityOperators.Contains(equalitySymbol.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, equals.OperatorToken.GetLocation(), "equality"));
            }
        }

        private static BinaryExpressionSyntax TryGetBinaryExpression(ExpressionSyntax expression)
        {
            var currentExpression = expression;
            while (currentExpression is ParenthesizedExpressionSyntax)
            {
                currentExpression = ((ParenthesizedExpressionSyntax) currentExpression).Expression;
            }

            return currentExpression as BinaryExpressionSyntax;
        }

        private static bool IsIndirectInequality(BinaryExpressionSyntax binaryExpression, BinaryExpressionSyntax right,
            BinaryExpressionSyntax left, SyntaxNodeAnalysisContext context)
        {
            return binaryExpression.IsKind(SyntaxKind.LogicalOrExpression) &&
                   HasAppropriateOperatorsForInequality(right, left) &&
                   HasFloatingType(right.Right, right.Left, context.SemanticModel);
        }

        private static bool IsIndirectEquality(BinaryExpressionSyntax binaryExpression, BinaryExpressionSyntax right,
            BinaryExpressionSyntax left, SyntaxNodeAnalysisContext context)
        {
            return binaryExpression.IsKind(SyntaxKind.LogicalAndExpression) &&
                   HasAppropriateOperatorsForEquality(right, left) &&
                   HasFloatingType(right.Right, right.Left, context.SemanticModel);
        }

        private static bool HasFloatingType(ExpressionSyntax right, ExpressionSyntax left, SemanticModel semanticModel)
        {
            return IsExpressionFloatingType(right, semanticModel) ||
                IsExpressionFloatingType(left, semanticModel);
        }

        private static bool IsExpressionFloatingType(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            return semanticModel.GetTypeInfo(expression).Type.IsAny(KnownType.FloatingPointNumbers);
        }

        private static bool HasAppropriateOperatorsForEquality(BinaryExpressionSyntax right, BinaryExpressionSyntax left)
        {
            return new[] {right.OperatorToken.Kind(), left.OperatorToken.Kind()}
                .Intersect(new[] {SyntaxKind.LessThanEqualsToken, SyntaxKind.GreaterThanEqualsToken})
                .Count() == 2;
        }
        private static bool HasAppropriateOperatorsForInequality(BinaryExpressionSyntax right, BinaryExpressionSyntax left)
        {
            return new[] { right.OperatorToken.Kind(), left.OperatorToken.Kind() }
                .Intersect(new[] { SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken })
                .Count() == 2;
        }
    }
}
