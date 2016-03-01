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

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class BooleanCheckInverted : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1940";
        internal const string Title = "Boolean checks should not be inverted";
        internal const string Description =
            "It is needlessly complex to invert the result of a boolean comparison. The opposite comparison should " +
            "be made instead.";
        internal const string MessageFormat = "Use the opposite operator (\"{0}\") instead.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
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
                    var expression = (BinaryExpressionSyntax) c.Node;
                    var enclosingSymbol = c.SemanticModel.GetEnclosingSymbol(expression.SpanStart) as IMethodSymbol;

                    if (enclosingSymbol != null &&
                        enclosingSymbol.MethodKind == MethodKind.UserDefinedOperator)
                    {
                        return;
                    }

                    var parenthesizedParent = expression.Parent;
                    while (parenthesizedParent is ParenthesizedExpressionSyntax)
                    {
                        parenthesizedParent = parenthesizedParent.Parent;
                    }

                    var logicalNot = parenthesizedParent as PrefixUnaryExpressionSyntax;
                    if (logicalNot != null && logicalNot.OperatorToken.IsKind(SyntaxKind.ExclamationToken))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, logicalNot.GetLocation(),
                            OppositeTokens[expression.OperatorToken.Kind()]));
                    }
                },
                SyntaxKind.GreaterThanExpression,
                SyntaxKind.GreaterThanOrEqualExpression,
                SyntaxKind.LessThanExpression,
                SyntaxKind.LessThanOrEqualExpression,
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);
        }

        private static readonly Dictionary<SyntaxKind, string> OppositeTokens =
            new Dictionary<SyntaxKind, string>
            {
                {SyntaxKind.GreaterThanToken, "<="},
                {SyntaxKind.GreaterThanEqualsToken, "<"},
                {SyntaxKind.LessThanToken, ">="},
                {SyntaxKind.LessThanEqualsToken, ">"},
                {SyntaxKind.EqualsEqualsToken, "!="},
                {SyntaxKind.ExclamationEqualsToken, "=="}
            };
    }
}
