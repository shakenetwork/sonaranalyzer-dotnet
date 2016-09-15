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
using System;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class ShiftDynamicNotInteger : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3449";
        internal const string Title = "Right operands of shift operators should be integers";
        internal const string Description =
            "Numbers can be shifted with the \"<<\" and \">>\" operators, but the right operand of the operation needs to be an \"int\" or a type " +
            "that has an implicit conversion to \"int\". However, with \"dynamic\", the compiler's type checking is turned off, so you can pass " +
            "anything to a shift operator and have it compile. And if the argument can't be converted to \"int\" at runtime, then a " +
            "\"RuntimeBinderException\" will be raised.";
        internal const string MessageFormat = "Remove this erroneous shift, it will fail because \"{0}\" can't be implicitly converted to \"int\".";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Blocker;
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
                c => CheckExpressionWithTwoParts<BinaryExpressionSyntax>(c.Node, b => b.Left, b => b.Right, c),
                SyntaxKind.LeftShiftExpression,
                SyntaxKind.RightShiftExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckExpressionWithTwoParts<AssignmentExpressionSyntax>(c.Node, b => b.Left, b => b.Right, c),
                SyntaxKind.LeftShiftAssignmentExpression,
                SyntaxKind.RightShiftAssignmentExpression);
        }

        private static void CheckExpressionWithTwoParts<T>(SyntaxNode node, Func<T, ExpressionSyntax> leftSelector, Func<T, ExpressionSyntax> rightSelector,
            SyntaxNodeAnalysisContext context)
            where T : SyntaxNode
        {
            var nodeWithTwoSides = (T)node;
            var left = leftSelector(nodeWithTwoSides);
            var right = rightSelector(nodeWithTwoSides);

            ITypeSymbol typeOfRight;
            if (IsDynamic(left, context.SemanticModel) &&
                !MightBeConvertibleToInt(right, context.SemanticModel, out typeOfRight))
            {
                var typeInMessage = GetTypeNameForMessage(right, typeOfRight, context.SemanticModel);

                context.ReportDiagnostic(Diagnostic.Create(Rule, right.GetLocation(),
                    typeInMessage));
            }
        }

        private static string GetTypeNameForMessage(ExpressionSyntax expression, ITypeSymbol typeOfRight, SemanticModel semanticModel)
        {
            var constValue = semanticModel.GetConstantValue(expression);
            return constValue.HasValue && constValue.Value == null
                ? "null"
                : typeOfRight.ToMinimalDisplayString(semanticModel, expression.SpanStart);
        }

        private static bool MightBeConvertibleToInt(ExpressionSyntax expression, SemanticModel semanticModel, out ITypeSymbol type)
        {
            type = semanticModel.GetTypeInfo(expression).Type;
            if (type is IErrorTypeSymbol)
            {
                return true;
            }

            var intType = semanticModel.Compilation.GetTypeByMetadataName("System.Int32");
            if (intType == null)
            {
                return false;
            }

            var conversion = semanticModel.ClassifyConversion(expression, intType);
            return conversion.Exists && (conversion.IsIdentity || conversion.IsImplicit);
        }

        private static bool IsDynamic(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var type = semanticModel.GetTypeInfo(expression).Type;
            return type != null && type.TypeKind == TypeKind.Dynamic;
        }
    }
}
