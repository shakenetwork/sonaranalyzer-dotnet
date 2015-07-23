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

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("bug")]
    public class SillyBitwiseOperation : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2437";
        internal const string Title = "Silly bit operations should not be performed";
        internal const string Description =
            "Certain bit operations are just silly and should not be performed because their results are predictable. " +
            "Specifically, using \"& -1\" with any value will always result in the original value, as will \"anyValue ^ 0\" " +
            "and \"anyValue | 0\".";
        internal const string MessageFormat = "Remove this silly bit operation.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);
        
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c => CheckBinary(c, -1),
                SyntaxKind.BitwiseAndExpression);

            context.RegisterSyntaxNodeAction(
                c => CheckBinary(c, 0),
                SyntaxKind.BitwiseOrExpression,
                SyntaxKind.ExclusiveOrExpression);

            context.RegisterSyntaxNodeAction(
                c => CheckAssignment(c, -1),
                SyntaxKind.AndAssignmentExpression);

            context.RegisterSyntaxNodeAction(
                c => CheckAssignment(c, 0),
                SyntaxKind.OrAssignmentExpression,
                SyntaxKind.ExclusiveOrAssignmentExpression);
        }

        private static void CheckAssignment(SyntaxNodeAnalysisContext c, int constValueToLookFor)
        {
            var assignment = (AssignmentExpressionSyntax)c.Node;
            int constValue;
            if (TryGetConstantIntValue(assignment.Right, out constValue) && 
                constValue == constValueToLookFor)
            {
                var location = GetReportLocation(assignment.OperatorToken.Span, assignment.Right.Span, assignment.SyntaxTree);
                c.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        }

        private static void CheckBinary(SyntaxNodeAnalysisContext c, int constValueToLookFor)
        {
            var binary = (BinaryExpressionSyntax) c.Node;
            int constValue;
            if (TryGetConstantIntValue(binary.Left, out constValue) &&
                constValue == constValueToLookFor)
            {
                var location = GetReportLocation(binary.Left.Span, binary.OperatorToken.Span, binary.SyntaxTree);
                c.ReportDiagnostic(Diagnostic.Create(Rule, location));
                return;
            }
            
            if (TryGetConstantIntValue(binary.Right, out constValue) &&
                constValue == constValueToLookFor)
            {
                var location = GetReportLocation(binary.OperatorToken.Span, binary.Right.Span, binary.SyntaxTree);
                c.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        }

        private static Location GetReportLocation(TextSpan start, TextSpan end, SyntaxTree tree)
        {
            return Location.Create(tree, new TextSpan(start.Start, end.End - start.Start));
        }

        private static bool TryConvert(object o, out int value)
        {
            try
            {
                value = Convert.ToInt32(o);
                return true;
            }
            catch (Exception exception)
            {
                if (exception is FormatException ||
                    exception is OverflowException ||
                    exception is InvalidCastException)
                {
                    value = 0;
                    return false;
                }

                throw;
            }
        }

        internal static bool TryGetConstantIntValue(ExpressionSyntax expression, out int value)
        {
            var multiplier = 1;
            var expr = expression;
            var unary = expr as PrefixUnaryExpressionSyntax;
            while (unary != null)
            {
                var op = unary.OperatorToken;

                if (!SupportedOperatorTokens.Contains(op.Kind()))
                {
                    value = 0;
                    return false;
                }

                if (op.IsKind(SyntaxKind.MinusToken))
                {
                    multiplier *= -1;
                }
                expr = unary.Operand;
                unary = expr as PrefixUnaryExpressionSyntax;
            }

            var literalExpression = expr as LiteralExpressionSyntax;
            if (literalExpression != null &&
                TryConvert(literalExpression.Token.Value, out value))
            {
                value = multiplier*value;
                return true;
            }

            value = 0;
            return false;
        }

        private static readonly SyntaxKind[] SupportedOperatorTokens =
        {
            SyntaxKind.MinusToken,
            SyntaxKind.PlusToken
        };
    }
}
