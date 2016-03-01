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
using System;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicChangeability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.BrainOverload)]
    public class FunctionNestingDepth : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S134";
        internal const string Title = "Control flow statements \"if\", \"for\", \"foreach\", \"do\", \"while\", \"switch\" and \"try\" should not be nested too deeply";
        internal const string Description =
           "Nested \"if\", \"switch\", \"for\", \"foreach\", \"while\", \"do\", and \"try\" statements are key ingredients for making what's known as \"Spaghetti code\". " +
           "Such code is hard to read, refactor and therefore maintain.";
        internal const string MessageFormat = "Refactor this code to not nest more than {0} control flow statements";
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

        [RuleParameter("max", PropertyType.Integer,
            "Maximum allowed control flow statement nesting depth", DefaultValueMaximum)]
        public int Maximum { get; set; } = DefaultValueMaximum;

        private static readonly SyntaxKind[] FunctionKinds =
        {
            SyntaxKind.MethodDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DestructorDeclaration,
            SyntaxKind.GetAccessorDeclaration,
            SyntaxKind.SetAccessorDeclaration,
            SyntaxKind.AddAccessorDeclaration,
            SyntaxKind.RemoveAccessorDeclaration
        };

        protected override void Initialize(ParameterLoadingAnalysisContext context) =>
            context.RegisterSyntaxNodeActionInNonGenerated(c => CheckFunctionNestingDepth(c), FunctionKinds);

        private void CheckFunctionNestingDepth(SyntaxNodeAnalysisContext context)
        {
            var walker = new NestingDepthWalker(Maximum, (token) => context.ReportDiagnostic(Diagnostic.Create(Rule, token.GetLocation(), Maximum)));
            walker.Visit(context.Node);
        }

        private class NestingDepthWalker : CSharpSyntaxWalker
        {
            private readonly int maximumNestingDepth;
            private readonly Action<SyntaxToken> actionMaximumExceeded;
            private int currentDepth = 0;

            public NestingDepthWalker(int maximumNestingDepth, Action<SyntaxToken> actionMaximumExceeded)
            {
                this.maximumNestingDepth = maximumNestingDepth;
                this.actionMaximumExceeded = actionMaximumExceeded;
            }

            public override void VisitIfStatement(IfStatementSyntax node)
            {
                var isPartOfChainedElseIfClause = node.Parent != null && node.Parent.IsKind(SyntaxKind.ElseClause);
                if (isPartOfChainedElseIfClause)
                {
                    base.VisitIfStatement(node);
                }
                else
                {
                    CheckNesting(node.IfKeyword, () => base.VisitIfStatement(node));
                }
            }

            public override void VisitForStatement(ForStatementSyntax node) => CheckNesting(node.ForKeyword, () => base.VisitForStatement(node));

            public override void VisitForEachStatement(ForEachStatementSyntax node) => CheckNesting(node.ForEachKeyword, () => base.VisitForEachStatement(node));

            public override void VisitWhileStatement(WhileStatementSyntax node) => CheckNesting(node.WhileKeyword, () => base.VisitWhileStatement(node));

            public override void VisitDoStatement(DoStatementSyntax node) => CheckNesting(node.DoKeyword, () => base.VisitDoStatement(node));

            public override void VisitSwitchStatement(SwitchStatementSyntax node) => CheckNesting(node.SwitchKeyword, () => base.VisitSwitchStatement(node));

            public override void VisitTryStatement(TryStatementSyntax node) => CheckNesting(node.TryKeyword, () => base.VisitTryStatement(node));

            private void CheckNesting(SyntaxToken keyword, Action visitAction)
            {
                currentDepth++;

                if (currentDepth <= maximumNestingDepth)
                {
                    visitAction();
                }
                else
                {
                    actionMaximumExceeded(keyword);
                }

                currentDepth--;
            }
        }
    }
}