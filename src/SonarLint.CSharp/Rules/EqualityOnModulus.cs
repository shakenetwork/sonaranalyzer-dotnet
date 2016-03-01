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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious)]
    public class EqualityOnModulus : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2197";
        internal const string Title = "Modulus results should not be checked for direct equality";
        internal const string Description =
            "When the modulus of a negative number is calculated, the result will either be negative or zero. Thus, comparing " +
            "the modulus of a variable for equality with a positive number (or a negative one) could result in unexpected results.";
        internal const string MessageFormat = "The result of this modulus operation may not be {0}.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

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
                    var equalsExpression = (BinaryExpressionSyntax)c.Node;

                    int constantValue;
                    if (CheckExpression(equalsExpression.Left, equalsExpression.Right, c.SemanticModel, out constantValue) ||
                        CheckExpression(equalsExpression.Right, equalsExpression.Left, c.SemanticModel, out constantValue))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, equalsExpression.GetLocation(),
                            constantValue < 0 ? "negative" : "positive"));
                    }
                },
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);
        }

        private static bool CheckExpression(ExpressionSyntax constant, ExpressionSyntax modulus, SemanticModel semanticModel,
            out int constantValue)
        {
            return ExpressionNumericConverter.TryGetConstantIntValue(constant, out constantValue) &&
                   constantValue != 0 &&
                   ExpressionIsModulus(modulus) &&
                   !ExpressionIsNonNegative(modulus, semanticModel);
        }

        private static bool ExpressionIsModulus(ExpressionSyntax expression)
        {
            var currentExpression = expression;
            var parenthesized = currentExpression as ParenthesizedExpressionSyntax;
            while (parenthesized != null)
            {
                currentExpression = parenthesized.Expression;
                parenthesized = currentExpression as ParenthesizedExpressionSyntax;
            }

            var binary = currentExpression as BinaryExpressionSyntax;
            return binary != null && binary.IsKind(SyntaxKind.ModuloExpression);
        }
        private static bool ExpressionIsNonNegative(ExpressionSyntax expression, SemanticModel semantic)
        {
            var type = semantic.GetTypeInfo(expression).Type;
            return type.IsAny(KnownType.UnsignedIntegers);
        }
    }
}
