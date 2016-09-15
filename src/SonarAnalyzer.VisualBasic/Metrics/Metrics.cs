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


using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarAnalyzer.Common.VisualBasic
{
    public class Metrics : MetricsBase
    {
        public Metrics(SyntaxTree tree) : base(tree)
        {
            var root = tree.GetRoot();
            if (root.Language != LanguageNames.VisualBasic)
            {
                throw new ArgumentException(InitalizationErrorTextPattern, nameof(tree));
            }
        }

        protected override Func<string, bool> HasValidCommentContent => line => line.Any(char.IsLetter) || line.Any(char.IsDigit);
        protected override Func<SyntaxToken, bool> IsEndOfFile => token => token.IsKind(SyntaxKind.EndOfFileToken);
        protected override Func<SyntaxToken, bool> IsNoneToken => token => token.IsKind(SyntaxKind.None);
        protected override Func<SyntaxTrivia, bool> IsCommentTrivia => trivia => TriviaKinds.Contains(trivia.Kind());
        protected override Func<SyntaxNode, bool> IsClass => node => ClassKinds.Contains(node.Kind());
        protected override Func<SyntaxNode, bool> IsAccessor => node => AccessorKinds.Contains(node.Kind());
        protected override Func<SyntaxNode, bool> IsStatement => node => node is ExecutableStatementSyntax;
        protected override Func<SyntaxNode, bool> IsFunction => node => FunctionKinds.Contains(node.Kind());
        protected override Func<SyntaxNode, bool> IsFunctionWithBody => node =>
            IsFunction(node) &&
            MethodBlocks.Contains(node.Parent.Kind());

        protected override IEnumerable<SyntaxNode> PublicApiNodes => new SyntaxNode[0]; // not calculated for VB.Net

        public override int GetComplexity(SyntaxNode node)
        {
            var possibleNodes = node
                    .DescendantNodesAndSelf()
                    .Where(n =>
                        !n.Parent.IsKind(SyntaxKind.InterfaceBlock) &&
                        ComplexityIncreasingKinds.Contains(n.Kind()));

            return possibleNodes.Count(n => !IsFunctionLikeLastReturnStatement(n));
        }
        private static bool IsFunctionLikeLastReturnStatement(SyntaxNode node)
        {
            if (!BranchKinds.Contains(node.Kind()))
            {
                return false;
            }

            if (!node.IsKind(SyntaxKind.ReturnStatement))
            {
                return false;
            }

            var blockType = node.Parent.GetType();
            if (!BlockTypes.Any(bType => bType.IsAssignableFrom(blockType)))
            {
                return false;
            }

            if (!IsFunctionLike(node.Parent))
            {
                return false;
            }

            var nextToken = node.GetLastToken().GetNextToken();
            var nextNode = nextToken.Parent;

            return nextToken.IsKind(SyntaxKind.EndKeyword) &&
                node.Parent == nextNode.Parent;
        }

        private static Func<SyntaxNode, bool> IsFunctionLike =>
            node => node.IsKind(SyntaxKind.FunctionBlock) ||
                node.IsKind(SyntaxKind.MultiLineFunctionLambdaExpression) ||
                node.IsKind(SyntaxKind.SingleLineFunctionLambdaExpression) ||
                node.IsKind(SyntaxKind.GetAccessorBlock);


        private static readonly SyntaxKind[] TriviaKinds =
        {
            SyntaxKind.CommentTrivia,
            SyntaxKind.DocumentationCommentExteriorTrivia,
            SyntaxKind.DocumentationCommentTrivia
        };
        private static readonly SyntaxKind[] ClassKinds =
        {
            SyntaxKind.ClassBlock,
            SyntaxKind.InterfaceBlock
            //todo: this should also be considered as a class, but it was not in the VB.Net plugin
            //SyntaxKind.ModuleBlock
        };
        private static readonly SyntaxKind[] AccessorKinds =
        {
            SyntaxKind.GetAccessorStatement,
            SyntaxKind.SetAccessorStatement
            //todo not included in VB.NET plugin
            //SyntaxKind.RaiseEventAccessorStatement,
            //SyntaxKind.AddHandlerAccessorStatement,
            //SyntaxKind.RemoveHandlerAccessorStatement
        };
        private static readonly SyntaxKind[] FunctionKinds =
        {
            SyntaxKind.SubNewStatement,
            SyntaxKind.SubStatement,
            SyntaxKind.FunctionStatement,
            // todo not counted in the VB.Net plugin
            //SyntaxKind.OperatorStatement,
            //SyntaxKind.GetAccessorStatement,
            //SyntaxKind.SetAccessorStatement,
            //SyntaxKind.RaiseEventAccessorStatement,
            //SyntaxKind.AddHandlerAccessorStatement,
            //SyntaxKind.RemoveHandlerAccessorStatement,
            SyntaxKind.DeclareSubStatement,
            SyntaxKind.DeclareFunctionStatement
        };
        private static readonly SyntaxKind[] MethodBlocks =
        {
            SyntaxKind.ConstructorBlock,
            SyntaxKind.FunctionBlock,
            SyntaxKind.SubBlock,
            SyntaxKind.OperatorBlock
            //SyntaxKind.GetAccessorBlock,
            //SyntaxKind.SetAccessorBlock,
            //SyntaxKind.RaiseEventAccessorBlock,
            //SyntaxKind.AddHandlerAccessorBlock,
            //SyntaxKind.RemoveHandlerAccessorBlock
        };
        private static readonly Type[] BlockTypes =
        {
            typeof(MethodBlockBaseSyntax),
            typeof(CaseBlockSyntax),
            typeof(CatchBlockSyntax),
            typeof(DoLoopBlockSyntax),
            typeof(ElseBlockSyntax),
            typeof(ElseIfBlockSyntax),
            typeof(EventBlockSyntax),
            typeof(FinallyBlockSyntax),
            typeof(ForOrForEachBlockSyntax),
            typeof(MultiLineIfBlockSyntax),
            typeof(SyncLockBlockSyntax),
            typeof(TryBlockSyntax),
            typeof(UsingBlockSyntax),
            typeof(WhileBlockSyntax),
            typeof(WithBlockSyntax),
            typeof(MultiLineLambdaExpressionSyntax)
        };
        private static readonly SyntaxKind[] BranchKinds = {
            SyntaxKind.GoToStatement,

            SyntaxKind.ExitDoStatement,
            SyntaxKind.ExitForStatement,
            SyntaxKind.ExitFunctionStatement,
            //not in VB.Net plugin
            //SyntaxKind.ExitOperatorStatement,
            SyntaxKind.ExitPropertyStatement,
            SyntaxKind.ExitSelectStatement,
            SyntaxKind.ExitSubStatement,
            SyntaxKind.ExitTryStatement,
            SyntaxKind.ExitWhileStatement,

            SyntaxKind.ContinueDoStatement,
            SyntaxKind.ContinueForStatement,
            SyntaxKind.ContinueWhileStatement,

            SyntaxKind.StopStatement,

            SyntaxKind.ReturnStatement,

            SyntaxKind.AndAlsoExpression,
            SyntaxKind.OrElseExpression,

            SyntaxKind.EndStatement
        };
        private static readonly SyntaxKind[] ComplexityIncreasingKinds =
        {
            SyntaxKind.SubStatement,
            SyntaxKind.FunctionStatement,
            SyntaxKind.DeclareSubStatement,
            SyntaxKind.DeclareFunctionStatement,
            SyntaxKind.SubNewStatement,

            SyntaxKind.IfStatement,
            SyntaxKind.SingleLineIfStatement,
            SyntaxKind.CaseStatement,

            SyntaxKind.WhileStatement,
            SyntaxKind.DoWhileStatement,
            SyntaxKind.DoUntilStatement,
            SyntaxKind.SimpleDoStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,

            SyntaxKind.ThrowStatement,
            SyntaxKind.TryStatement,

            SyntaxKind.ErrorStatement,

            SyntaxKind.ResumeStatement,
            SyntaxKind.ResumeNextStatement,
            SyntaxKind.ResumeLabelStatement,

            SyntaxKind.OnErrorGoToLabelStatement,
            SyntaxKind.OnErrorGoToMinusOneStatement,
            SyntaxKind.OnErrorGoToZeroStatement,
            SyntaxKind.OnErrorResumeNextStatement,

            SyntaxKind.GoToStatement,

            SyntaxKind.ExitDoStatement,
            SyntaxKind.ExitForStatement,
            SyntaxKind.ExitFunctionStatement,
            //not in VB.Net plugin
            //SyntaxKind.ExitOperatorStatement,
            SyntaxKind.ExitPropertyStatement,
            SyntaxKind.ExitSelectStatement,
            SyntaxKind.ExitSubStatement,
            SyntaxKind.ExitTryStatement,
            SyntaxKind.ExitWhileStatement,

            SyntaxKind.ContinueDoStatement,
            SyntaxKind.ContinueForStatement,
            SyntaxKind.ContinueWhileStatement,

            SyntaxKind.StopStatement,

            SyntaxKind.ReturnStatement,

            SyntaxKind.AndAlsoExpression,
            SyntaxKind.OrElseExpression,

            SyntaxKind.EndStatement
        };
    }
}
