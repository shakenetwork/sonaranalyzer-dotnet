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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class UnaryPrefixOperatorRepeated : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2761";
        internal const string Title = "Doubled prefix operators \"!!\" and \"~~\" should not be used";
        internal const string Description =
            "Calling the \"!\" or \"~\" prefix operator twice does nothing: the second invocation undoes the first. " +
            "Such mistakes are typically caused by accidentally double-tapping the key in question without noticing.";
        internal const string MessageFormat = "Use the \"{0}\" operator just once or not at all.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
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
                    var topLevelUnary = (PrefixUnaryExpressionSyntax)c.Node;

                    if (!TopLevelUnaryInChain(topLevelUnary))
                    {
                        return;
                    }

                    var repeatedCount = 0U;
                    var currentUnary = topLevelUnary;
                    var lastUnary = currentUnary;
                    while (currentUnary != null &&
                           SameOperators(currentUnary, topLevelUnary))
                    {
                        lastUnary = currentUnary;
                        repeatedCount++;
                        currentUnary = currentUnary.Operand as PrefixUnaryExpressionSyntax;
                    }

                    if (repeatedCount < 2)
                    {
                        return;
                    }

                    var errorLocation = new TextSpan(topLevelUnary.SpanStart, lastUnary.OperatorToken.Span.End - topLevelUnary.SpanStart);
                    c.ReportDiagnostic(Diagnostic.Create(Rule,
                            Location.Create(c.Node.SyntaxTree, errorLocation),
                            topLevelUnary.OperatorToken.ToString()));
                },
                SyntaxKind.LogicalNotExpression,
                SyntaxKind.BitwiseNotExpression);
        }

        private static bool TopLevelUnaryInChain(PrefixUnaryExpressionSyntax unary)
        {
            var parent = unary.Parent as PrefixUnaryExpressionSyntax;
            return parent == null || !SameOperators(parent, unary);
        }

        private static bool SameOperators(PrefixUnaryExpressionSyntax expression1, PrefixUnaryExpressionSyntax expression2)
        {
            return expression1.OperatorToken.IsKind(expression2.OperatorToken.Kind());
        }
    }
}
