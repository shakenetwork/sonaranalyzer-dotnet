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

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("30min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.UnitTestability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.BrainOverload)]
    public class ExpressionComplexity : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1067";
        internal const string Title = "Expressions should not be too complex";
        internal const string Description =
           "The complexity of an expression is defined by the number of \"&&\", \"||\" and \"condition ? ifTrue : ifFalse\" operators it contains. " +
            "A single expression's complexity should not become too high to keep the code readable.";
        internal const string MessageFormat = "Reduce the number of conditional operators ({1}) used in the expression (maximum allowed {0}).";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const int DefaultValueMaximum = 3;

        [RuleParameter("max", PropertyType.Integer, "Maximum number of allowed conditional operators in an expression", DefaultValueMaximum)]
        public int Maximum { get; set; } = DefaultValueMaximum;

        private static readonly IImmutableSet<SyntaxKind> CompoundExpressionKinds = ImmutableHashSet.Create(
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ArrayInitializerExpression,
            SyntaxKind.AnonymousMethodExpression,
            SyntaxKind.ObjectInitializerExpression,
            SyntaxKind.InvocationExpression);

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    var root = c.Tree.GetRoot();

                    var rootExpressions =
                        root
                        .DescendantNodes(e2 => !(e2 is ExpressionSyntax))
                        .Where(
                            e =>
                                e is ExpressionSyntax &&
                                !IsCompoundExpression(e));

                    var compoundExpressionsDescendants =
                        root
                        .DescendantNodes()
                        .Where(IsCompoundExpression)
                        .SelectMany(
                            e =>
                                e
                                .DescendantNodes(
                                    e2 =>
                                        e == e2 ||
                                        !(e2 is ExpressionSyntax))
                                .Where(
                                    e2 =>
                                        e2 is ExpressionSyntax &&
                                        !IsCompoundExpression(e2)));

                    var expressionsToCheck = rootExpressions.Concat(compoundExpressionsDescendants);

                    var complexExpressions =
                        expressionsToCheck
                        .Select(
                            e =>
                            new
                            {
                                Expression = e,
                                Complexity =
                                    e
                                    .DescendantNodesAndSelf(e2 => !IsCompoundExpression(e2))
                                    .Count(
                                        e2 =>
                                            e2.IsKind(SyntaxKind.ConditionalExpression) ||
                                            e2.IsKind(SyntaxKind.LogicalAndExpression) ||
                                            e2.IsKind(SyntaxKind.LogicalOrExpression))
                            })
                        .Where(e => e.Complexity > Maximum);

                    foreach (var complexExpression in complexExpressions)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, complexExpression.Expression.GetLocation(), Maximum, complexExpression.Complexity));
                    }
                });
        }

        private static bool IsCompoundExpression(SyntaxNode node)
        {
            return CompoundExpressionKinds.Any(node.IsKind);
        }
    }
}
