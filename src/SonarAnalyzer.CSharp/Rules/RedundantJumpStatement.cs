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
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using SonarAnalyzer.Helpers.FlowAnalysis.CSharp;
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("1min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    [Tags(Tag.Clumsy, Tag.Finding)]
    public class RedundantJumpStatement : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3626";
        internal const string Title = "Jump statements should not be redundant";
        internal const string Description =
            "Jump statements, such as \"return\", \"yield break\", \"goto\", and \"continue\" let you change the default flow of program execution, but jump " +
            "statements that direct the control flow to the original direction are just a waste of keystrokes.";
        internal const string MessageFormat = "Remove this redundant jump.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), true,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (BaseMethodDeclarationSyntax)c.Node;
                    CheckForRedundantJumps(declaration.Body, c);
                },
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ConstructorDeclaration,
                SyntaxKind.DestructorDeclaration,
                SyntaxKind.ConversionOperatorDeclaration,
                SyntaxKind.OperatorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (AccessorDeclarationSyntax)c.Node;
                    CheckForRedundantJumps(declaration.Body, c);
                },
                SyntaxKind.GetAccessorDeclaration,
                SyntaxKind.SetAccessorDeclaration,
                SyntaxKind.AddAccessorDeclaration,
                SyntaxKind.RemoveAccessorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (AnonymousFunctionExpressionSyntax)c.Node;
                    CheckForRedundantJumps(declaration.Body, c);
                },
                SyntaxKind.AnonymousMethodExpression,
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression);
        }

        private static void CheckForRedundantJumps(CSharpSyntaxNode node, SyntaxNodeAnalysisContext context)
        {
            IControlFlowGraph cfg;
            if (!ControlFlowGraph.TryGet(node, context.SemanticModel, out cfg))
            {
                return;
            }

            var yieldStatementCount = node.DescendantNodes().OfType<YieldStatementSyntax>().Count();

            var removableJumps = cfg.Blocks
                .OfType<JumpBlock>()
                .Where(jumpBlock => IsJumpRemovable(jumpBlock, yieldStatementCount));

            foreach (var jumpBlock in removableJumps)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, jumpBlock.JumpNode.GetLocation()));
            }
        }

        private static bool IsJumpRemovable(JumpBlock jumpBlock, int yieldStatementCount)
        {
            return !IsInsideSwitch(jumpBlock) &&
                   !IsReturnWithExpression(jumpBlock) &&
                   !IsThrow(jumpBlock) &&
                   !IsOnlyYieldBreak(jumpBlock, yieldStatementCount) &&
                   jumpBlock.SuccessorBlock == jumpBlock.WouldBeSuccessor;
        }

        private static bool IsInsideSwitch(JumpBlock jumpBlock)
        {
            // Not reporting inside switch, as the jumps might not be removable
            return jumpBlock.JumpNode.AncestorsAndSelf().OfType<SwitchStatementSyntax>().Any();
        }

        private static bool IsOnlyYieldBreak(JumpBlock jumpBlock, int yieldStatementCount)
        {
            var yieldStatement = jumpBlock.JumpNode as YieldStatementSyntax;
            return yieldStatement != null && yieldStatementCount == 1;
        }

        private static bool IsThrow(JumpBlock jumpBlock)
        {
            var throwStatement = jumpBlock.JumpNode as ThrowStatementSyntax;
            return throwStatement != null;
        }

        private static bool IsReturnWithExpression(JumpBlock jumpBlock)
        {
            var returnStatement = jumpBlock.JumpNode as ReturnStatementSyntax;
            return returnStatement != null && returnStatement.Expression != null;
        }
    }
}
