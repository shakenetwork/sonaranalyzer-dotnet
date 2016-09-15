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
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [Rule(DiagnosticId, RuleSeverity, Title, true)]
    public class ExpressionComplexity : ExpressionComplexityBase<ExpressionSyntax>
    {
        internal const string Description =
           "Complex boolean expressions are hard to read and so to maintain.";

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), false,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.VisualBasic.GeneratedCodeRecognizer.Instance;

        private static readonly ISet<SyntaxKind> CompoundExpressionKinds = ImmutableHashSet.Create(
            SyntaxKind.MultiLineFunctionLambdaExpression,
            SyntaxKind.MultiLineSubLambdaExpression,
            SyntaxKind.SingleLineFunctionLambdaExpression,
            SyntaxKind.SingleLineSubLambdaExpression,

            SyntaxKind.CollectionInitializer,
            SyntaxKind.ObjectMemberInitializer,

            SyntaxKind.InvocationExpression);

        private static readonly ISet<SyntaxKind> ComplexityIncreasingKinds = ImmutableHashSet.Create(
            SyntaxKind.AndExpression,
            SyntaxKind.AndAlsoExpression,
            SyntaxKind.OrExpression,
            SyntaxKind.OrElseExpression,
            SyntaxKind.ExclusiveOrExpression);

        protected override bool IsComplexityIncreasingKind(SyntaxNode node) =>
            ComplexityIncreasingKinds.Contains(node.Kind());

        protected override bool IsCompoundExpression(SyntaxNode node) =>
            CompoundExpressionKinds.Contains(node.Kind());
    }
}
