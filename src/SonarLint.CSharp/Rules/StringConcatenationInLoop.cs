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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Rules.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.MemoryEfficiency)]
    [Tags(Tag.Performance)]
    public class StringConcatenationInLoop
        : StringConcatenationInLoopBase<SyntaxKind, AssignmentExpressionSyntax, BinaryExpressionSyntax>
    {
        protected override bool ExpressionIsConcatenation(BinaryExpressionSyntax addExpression) =>
            addExpression.IsKind(SyntaxKind.AddExpression);

        protected override SyntaxNode GetLeft(AssignmentExpressionSyntax assignment) => assignment.Left;

        protected override SyntaxNode GetRight(AssignmentExpressionSyntax assignment) => assignment.Right;

        protected override SyntaxNode GetLeft(BinaryExpressionSyntax binary) => binary.Left;

        protected override bool IsInLoop(SyntaxNode node) => LoopKinds.Any(loopKind => node.IsKind(loopKind));

        protected override bool AreEquivalent(SyntaxNode node1, SyntaxNode node2) =>
            EquivalenceChecker.AreEquivalent(node1, node2);

        private static readonly SyntaxKind[] LoopKinds =
        {
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement
        };

        private static readonly ImmutableArray<SyntaxKind> simpleAssignmentKinds =
            ImmutableArray.Create(SyntaxKind.SimpleAssignmentExpression);

        private static readonly ImmutableArray<SyntaxKind> compoundAssignmentKinds =
            ImmutableArray.Create(SyntaxKind.AddAssignmentExpression);

        protected override ImmutableArray<SyntaxKind> SimpleAssignmentKinds => simpleAssignmentKinds;

        protected override ImmutableArray<SyntaxKind> CompoundAssignmentKinds => compoundAssignmentKinds;

        protected sealed override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.CSharp.GeneratedCodeRecognizer.Instance;
    }
}
