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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using SonarLint.Helpers.FlowAnalysis.Common;

namespace SonarLint.UnitTest.Helpers
{
    [TestClass]
    public class ControlFlowGraphTest
    {
        #region Top level - build CFG expression body / body

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Constructed_for_Body()
        {
            var cfg = Build("i = 4 + 5;");
            VerifyMinimalCfg(cfg);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Constructed_for_ExpressionBody()
        {
            const string input = @"
namespace NS
{
  public class Foo
  {
    public int Bar() => 4 + 5;
  }
}";

            SemanticModel semanticModel;
            var method = Compile(input, "Bar", out semanticModel);
            var expression = method.ExpressionBody.Expression;
            var cfg = SonarLint.Helpers.FlowAnalysis.CSharp.ControlFlowGraph.Create(expression, semanticModel);
            VerifyMinimalCfg(cfg);
        }

        #endregion

        #region Empty statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_EmptyStatement()
        {
            var cfg = Build(";;;;;");
            VerifyEmptyCfg(cfg);
        }

        #endregion

        #region Variable declaration

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_VariableDeclaration()
        {
            var cfg = Build("var x = 10, y = 11; var z = 12;");
            VerifyMinimalCfg(cfg);
        }

        #endregion

        #region If statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_If()
        {
            var cfg = Build("if (true) { var x = 10; }");

            VerifyCfg(cfg, 3);
            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var trueBlock = cfg.Blocks.ToList()[1];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(trueBlock, exitBlock);
            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.IfStatement);

            trueBlock.SuccessorBlocks.Should().OnlyContain(exitBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(branchBlock, trueBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_If_Else()
        {
            var cfg = Build("if (true) { var x = 10; } else { var y = 11; }");
            VerifyCfg(cfg, 4);
            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var trueBlock = cfg.Blocks.ToList()[1];
            var falseBlock = cfg.Blocks.ToList()[2];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(trueBlock, falseBlock);
            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.IfStatement);

            trueBlock.SuccessorBlocks.Should().OnlyContain(exitBlock);
            falseBlock.SuccessorBlocks.Should().OnlyContain(exitBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(trueBlock, falseBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_If_ElseIf()
        {
            var cfg = Build("if (true) { var x = 10; } else if (false) { var y = 11; }");
            VerifyCfg(cfg, 5);
            var firstCondition = cfg.EntryBlock as BinaryBranchBlock;
            var trueBlockX = cfg.Blocks.ToList()[1];
            var secondCondition = cfg.Blocks.ToList()[2] as BinaryBranchBlock;
            var trueBlockY = cfg.Blocks.ToList()[3];
            var exitBlock = cfg.ExitBlock;

            firstCondition.SuccessorBlocks.Should().OnlyContainInOrder(trueBlockX, secondCondition);
            firstCondition.BranchingNode.Kind().Should().Be(SyntaxKind.IfStatement);

            trueBlockX.SuccessorBlocks.Should().OnlyContain(exitBlock);

            secondCondition.SuccessorBlocks.Should().OnlyContainInOrder(trueBlockY, exitBlock);
            secondCondition.BranchingNode.Kind().Should().Be(SyntaxKind.IfStatement);

            exitBlock.PredecessorBlocks.Should().OnlyContain(trueBlockX, trueBlockY, secondCondition);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_If_ElseIf_Else()
        {
            var cfg = Build("if (true) { var x = 10; } else if (false) { var y = 11; } else { var z = 12; }");
            VerifyCfg(cfg, 6);
            var firstCondition = cfg.EntryBlock as BinaryBranchBlock;
            var trueBlockX = cfg.Blocks.ToList()[1];
            var secondCondition = cfg.Blocks.ToList()[2] as BinaryBranchBlock;
            var trueBlockY = cfg.Blocks.ToList()[3];
            var falseBlockZ = cfg.Blocks.ToList()[4];
            var exitBlock = cfg.ExitBlock;

            firstCondition.SuccessorBlocks.Should().OnlyContainInOrder(trueBlockX, secondCondition);
            firstCondition.BranchingNode.Kind().Should().Be(SyntaxKind.IfStatement);

            trueBlockX.SuccessorBlocks.Should().OnlyContain(exitBlock);

            secondCondition.SuccessorBlocks.Should().OnlyContainInOrder(trueBlockY, falseBlockZ);
            secondCondition.BranchingNode.Kind().Should().Be(SyntaxKind.IfStatement);

            trueBlockY.SuccessorBlocks.Should().OnlyContain(exitBlock);
            falseBlockZ.SuccessorBlocks.Should().OnlyContain(exitBlock);

            exitBlock.PredecessorBlocks.Should().OnlyContain(trueBlockX, trueBlockY, falseBlockZ);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_NestedIf()
        {
            var cfg = Build("if (true) { if (false) { var x = 10; } else { var y = 10; } }");
            VerifyCfg(cfg, 5);
            var firstCondition = cfg.EntryBlock as BinaryBranchBlock;
            firstCondition.Instructions.FirstOrDefault(n => n.IsKind(SyntaxKind.TrueLiteralExpression)).Should().NotBeNull();
            var secondCondition = cfg.Blocks.ToList()[1] as BinaryBranchBlock;
            secondCondition.Instructions.FirstOrDefault(n => n.IsKind(SyntaxKind.FalseLiteralExpression)).Should().NotBeNull();
            var trueBlockX = cfg.Blocks.ToList()[2];
            var falseBlockY = cfg.Blocks.ToList()[3];
            var exitBlock = cfg.ExitBlock;

            firstCondition.SuccessorBlocks.Should().OnlyContainInOrder(secondCondition, exitBlock);
            firstCondition.BranchingNode.Kind().Should().Be(SyntaxKind.IfStatement);

            secondCondition.SuccessorBlocks.Should().OnlyContainInOrder(trueBlockX, falseBlockY);
            secondCondition.BranchingNode.Kind().Should().Be(SyntaxKind.IfStatement);

            trueBlockX.SuccessorBlocks.Should().OnlyContain(exitBlock);
            falseBlockY.SuccessorBlocks.Should().OnlyContain(exitBlock);

            exitBlock.PredecessorBlocks.Should().OnlyContain(trueBlockX, falseBlockY, firstCondition);
        }

        #endregion

        #region While statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_While()
        {
            var cfg = Build("while (true) { var x = 10; }");
            VerifyCfg(cfg, 3);
            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var loopBodyBlock = cfg.Blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "x = 10"));
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, exitBlock);
            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.WhileStatement);

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(branchBlock);
            branchBlock.PredecessorBlocks.Should().OnlyContain(loopBodyBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(branchBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_NestedWhile()
        {
            var cfg = Build("while (true) while(false) { var x = 10; }");
            VerifyCfg(cfg, 4);
            var firstBranchBlock = cfg.EntryBlock as BinaryBranchBlock;
            firstBranchBlock.Instructions.FirstOrDefault(n => n.IsKind(SyntaxKind.TrueLiteralExpression)).Should().NotBeNull();
            var blocks = cfg.Blocks.ToList();
            var loopBodyBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "x = 10"));
            var secondBranchBlock = blocks[1] as BinaryBranchBlock;
            secondBranchBlock.Instructions.FirstOrDefault(n => n.IsKind(SyntaxKind.FalseLiteralExpression)).Should().NotBeNull();
            var exitBlock = cfg.ExitBlock;

            firstBranchBlock.SuccessorBlocks.Should().OnlyContainInOrder(secondBranchBlock, exitBlock);
            firstBranchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.WhileStatement);

            secondBranchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, firstBranchBlock);
            secondBranchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.WhileStatement);

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(secondBranchBlock);
            firstBranchBlock.PredecessorBlocks.Should().OnlyContain(secondBranchBlock);
            secondBranchBlock.PredecessorBlocks.Should().OnlyContain(firstBranchBlock, loopBodyBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(firstBranchBlock);
        }

        #endregion

        #region Do statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_DoWhile()
        {
            var cfg = Build("do { var x = 10; } while (true);");
            VerifyCfg(cfg, 3);
            var branchBlock = cfg.Blocks.ToList()[1] as BinaryBranchBlock;
            var loopBodyBlock = cfg.EntryBlock;
            var exitBlock = cfg.ExitBlock;

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(branchBlock);

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, exitBlock);
            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.DoStatement);

            branchBlock.PredecessorBlocks.Should().OnlyContain(loopBodyBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(new[] { branchBlock });
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_NestedDoWhile()
        {
            var cfg = Build("do { do { var x = 10; } while (false); } while (true);");
            VerifyCfg(cfg, 4);
            var blocks = cfg.Blocks.ToList();
            var loopBodyBlock = cfg.EntryBlock;
            var falseBranchBlock = blocks[1] as BinaryBranchBlock;
            falseBranchBlock.Instructions.FirstOrDefault(n => n.IsKind(SyntaxKind.FalseLiteralExpression)).Should().NotBeNull();
            var trueBranchBlock = blocks[2] as BinaryBranchBlock;
            trueBranchBlock.Instructions.FirstOrDefault(n => n.IsKind(SyntaxKind.TrueLiteralExpression)).Should().NotBeNull();
            var exitBlock = cfg.ExitBlock;

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(falseBranchBlock);

            falseBranchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, trueBranchBlock);
            falseBranchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.DoStatement);

            trueBranchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, exitBlock);
            trueBranchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.DoStatement);

            falseBranchBlock.PredecessorBlocks.Should().OnlyContain(loopBodyBlock);
            trueBranchBlock.PredecessorBlocks.Should().OnlyContain(falseBranchBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(trueBranchBlock);
        }

        #endregion

        #region Foreach statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Foreach()
        {
            var cfg = Build("foreach (var item in collection) { var x = 10; }");
            VerifyCfg(cfg, 3);
            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var loopBodyBlock = cfg.Blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "x = 10"));
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, exitBlock);
            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.ForEachStatement);

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(branchBlock);
            branchBlock.PredecessorBlocks.Should().OnlyContain(loopBodyBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(branchBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_NestedForeach()
        {
            var cfg = Build("foreach (var item1 in collection1) { foreach (var item2 in collection2) { var x = 10; } }");
            VerifyCfg(cfg, 4);
            var firstBranchBlock = cfg.EntryBlock as BinaryBranchBlock;
            firstBranchBlock.Instructions.FirstOrDefault(n => n.ToString() == "collection1").Should().NotBeNull();
            var blocks = cfg.Blocks.ToList();
            var loopBodyBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "x = 10"));
            var secondBranchBlock = blocks[1] as BinaryBranchBlock;
            secondBranchBlock.Instructions.FirstOrDefault(n => n.ToString() == "collection2").Should().NotBeNull();
            var exitBlock = cfg.ExitBlock;

            firstBranchBlock.SuccessorBlocks.Should().OnlyContainInOrder(secondBranchBlock, exitBlock);
            firstBranchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.ForEachStatement);

            secondBranchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, firstBranchBlock);
            secondBranchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.ForEachStatement);

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(secondBranchBlock);
            firstBranchBlock.PredecessorBlocks.Should().OnlyContain(secondBranchBlock);
            secondBranchBlock.PredecessorBlocks.Should().OnlyContain(firstBranchBlock, loopBodyBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(firstBranchBlock);
        }

        #endregion

        #region For statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_For()
        {
            var cfg = Build("for (var i = 0; true; i++) { var x = 10; }");
            VerifyForStatement(cfg);

            cfg = Build("var y = 11; for (var i = 0; true; i++) { var x = 10; }");
            VerifyForStatement(cfg);

            cfg = Build("for (var i = 0; ; i++) { var x = 10; }");
            VerifyForStatement(cfg);

            cfg = Build("for (i = 0, j = 11; ; i++) { var x = 10; }");
            VerifyForStatement(cfg);
        }

        private static void VerifyForStatement(IControlFlowGraph cfg)
        {
            VerifyCfg(cfg, 5);
            var initBlock = cfg.EntryBlock;
            var blocks = cfg.Blocks.ToList();
            var branchBlock = blocks[1] as BinaryBranchBlock;
            var incrementorBlock = blocks[3];
            var loopBodyBlock = blocks[2];
            var exitBlock = cfg.ExitBlock;

            initBlock.SuccessorBlocks.Should().OnlyContain(branchBlock);

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, exitBlock);
            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.ForStatement);

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(incrementorBlock);
            incrementorBlock.SuccessorBlocks.Should().OnlyContain(branchBlock);
            branchBlock.PredecessorBlocks.Should().OnlyContain(initBlock, incrementorBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(branchBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_For_NoInitializer()
        {
            var cfg = Build("for (; true; i++) { var x = 10; }");
            VerifyForStatementNoInitializer(cfg);

            cfg = Build("for (; ; i++) { var x = 10; }");
            VerifyForStatementNoInitializer(cfg);
        }

        private static void VerifyForStatementNoInitializer(IControlFlowGraph cfg)
        {
            VerifyCfg(cfg, 4);
            var blocks = cfg.Blocks.ToList();
            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var incrementorBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "i++"));
            var loopBodyBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "x = 10"));
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, exitBlock);
            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.ForStatement);

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(incrementorBlock);
            incrementorBlock.SuccessorBlocks.Should().OnlyContain(branchBlock);
            branchBlock.PredecessorBlocks.Should().OnlyContain(incrementorBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(branchBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_For_NoIncrementor()
        {
            var cfg = Build("for (var i = 0; true;) { var x = 10; }");
            VerifyForStatementNoIncrementor(cfg);

            cfg = Build("for (var i = 0; ;) { var x = 10; }");
            VerifyForStatementNoIncrementor(cfg);
        }

        private static void VerifyForStatementNoIncrementor(IControlFlowGraph cfg)
        {
            VerifyCfg(cfg, 4);
            var initBlock = cfg.EntryBlock;
            var blocks = cfg.Blocks.ToList();
            var branchBlock = blocks[1] as BinaryBranchBlock;
            var loopBodyBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "x = 10"));
            var exitBlock = cfg.ExitBlock;

            initBlock.SuccessorBlocks.Should().OnlyContain(branchBlock);

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, exitBlock);
            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.ForStatement);

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(branchBlock);

            branchBlock.PredecessorBlocks.Should().OnlyContain(initBlock, loopBodyBlock);

            exitBlock.PredecessorBlocks.Should().OnlyContain(branchBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_For_Empty()
        {
            var cfg = Build("for (; true;) { var x = 10; }");
            VerifyForStatementEmpty(cfg);

            cfg = Build("for (;;) { var x = 10; }");
            VerifyForStatementEmpty(cfg);
        }

        private static void VerifyForStatementEmpty(IControlFlowGraph cfg)
        {
            VerifyCfg(cfg, 3);
            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var loopBodyBlock = cfg.Blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "x = 10"));
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, exitBlock);
            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.ForStatement);

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(branchBlock);
            branchBlock.PredecessorBlocks.Should().OnlyContain(loopBodyBlock);
            exitBlock.PredecessorBlocks.Should().OnlyContain(branchBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_NestedFor()
        {
            var cfg = Build("for (var i = 0; true; i++) { for (var j = 0; false; j++) { var x = 10; } }");
            VerifyCfg(cfg, 8);
            var initBlockI = cfg.EntryBlock;
            var blocks = cfg.Blocks.ToList();
            var branchBlockTrue = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "true")) as BinaryBranchBlock;
            var incrementorBlockI = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "i++"));

            var branchBlockFalse = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "false")) as BinaryBranchBlock;
            var incrementorBlockJ = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "j++"));
            var initBlockJ = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "j = 0"));

            var loopBodyBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "x = 10"));
            var exitBlock = cfg.ExitBlock;

            initBlockI.SuccessorBlocks.Should().OnlyContain(branchBlockTrue);

            branchBlockTrue.SuccessorBlocks.Should().OnlyContainInOrder(initBlockJ, exitBlock);
            branchBlockTrue.BranchingNode.Kind().Should().Be(SyntaxKind.ForStatement);

            initBlockJ.SuccessorBlocks.Should().OnlyContain(branchBlockFalse);

            branchBlockFalse.SuccessorBlocks.Should().OnlyContainInOrder(loopBodyBlock, incrementorBlockI);
            branchBlockFalse.BranchingNode.Kind().Should().Be(SyntaxKind.ForStatement);

            loopBodyBlock.SuccessorBlocks.Should().OnlyContain(incrementorBlockJ);
            incrementorBlockJ.SuccessorBlocks.Should().OnlyContain(branchBlockFalse);
            incrementorBlockI.SuccessorBlocks.Should().OnlyContain(branchBlockTrue);

            exitBlock.PredecessorBlocks.Should().OnlyContain(branchBlockTrue);
        }

        #endregion

        #region Return, throw, yield break statement

        private const string SimpleReturn = "return";
        private const string SimpleThrow = "throw";
        private const string SimpleYieldBreak = "yield break";
        private const string ExpressionReturn = "return ii";
        private const string ExpressionThrow = "throw ii";

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Return()
        {
            var cfg = Build($"if (true) {{ var y = 12; {SimpleReturn}; }} var x = 11;");
            VerifyJumpWithNoExpression(cfg, SyntaxKind.ReturnStatement);

            cfg = Build($"if (true) {{ {SimpleReturn}; }} var x = 11;");
            VerifyJumpWithNoExpression(cfg, SyntaxKind.ReturnStatement);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Throw()
        {
            var cfg = Build($"if (true) {{ var y = 12; {SimpleThrow}; }} var x = 11;");
            VerifyJumpWithNoExpression(cfg, SyntaxKind.ThrowStatement);

            cfg = Build($"if (true) {{ {SimpleThrow}; }} var x = 11;");
            VerifyJumpWithNoExpression(cfg, SyntaxKind.ThrowStatement);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_YieldBreak()
        {
            var cfg = Build($"if (true) {{ var y = 12; {SimpleYieldBreak}; }} var x = 11;");
            VerifyJumpWithNoExpression(cfg, SyntaxKind.YieldBreakStatement);

            cfg = Build($"if (true) {{ {SimpleYieldBreak}; }} var x = 11;");
            VerifyJumpWithNoExpression(cfg, SyntaxKind.YieldBreakStatement);
        }

        private static void VerifyJumpWithNoExpression(IControlFlowGraph cfg, SyntaxKind kind)
        {
            VerifyCfg(cfg, 4);
            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var trueBlock = blocks[1] as JumpBlock;
            var falseBlock = blocks[2];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(trueBlock, falseBlock);
            trueBlock.SuccessorBlocks.Should().OnlyContain(exitBlock);
            trueBlock.JumpNode.Kind().Should().Be(kind);

            exitBlock.PredecessorBlocks.Should().OnlyContain(trueBlock, falseBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Return_Value()
        {
            var cfg = Build($"if (true) {{ var y = 12; {ExpressionReturn}; }} var x = 11;");
            VerifyJumpWithExpression(cfg, SyntaxKind.ReturnStatement);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Throw_Value()
        {
            var cfg = Build($"if (true) {{ var y = 12; {ExpressionThrow}; }} var x = 11;");
            VerifyJumpWithExpression(cfg, SyntaxKind.ThrowStatement);
        }

        private static void VerifyJumpWithExpression(IControlFlowGraph cfg, SyntaxKind kind)
        {
            VerifyCfg(cfg, 4);
            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var trueBlock = blocks[1] as JumpBlock;
            var falseBlock = blocks[2];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(trueBlock, falseBlock);
            trueBlock.SuccessorBlocks.Should().OnlyContain(exitBlock);
            trueBlock.JumpNode.Kind().Should().Be(kind);

            trueBlock.Instructions.FirstOrDefault(n => n.IsKind(SyntaxKind.IdentifierName) && n.ToString() == "ii").Should().NotBeNull();

            exitBlock.PredecessorBlocks.Should().OnlyContain(trueBlock, falseBlock);
        }

        #endregion

        #region Lock statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Lock()
        {
            var cfg = Build("lock(this) { var x = 10; }");
            VerifySimpleJumpBlock(cfg, SyntaxKind.LockStatement);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_NestedLock()
        {
            var cfg = Build("lock(this) { lock(that) { var x = 10; }}");
            VerifyCfg(cfg, 4);
            var jumpBlock = cfg.EntryBlock as JumpBlock;
            var blocks = cfg.Blocks.ToList();
            var innerJumpBlock = blocks[1] as JumpBlock;
            var bodyBlock = blocks[2];
            var exitBlock = cfg.ExitBlock;

            jumpBlock.SuccessorBlocks.Should().OnlyContain(innerJumpBlock);
            innerJumpBlock.SuccessorBlocks.Should().OnlyContain(bodyBlock);
            bodyBlock.SuccessorBlocks.Should().OnlyContain(exitBlock);

            jumpBlock.JumpNode.Kind().Should().Be(SyntaxKind.LockStatement);
            jumpBlock.Instructions.FirstOrDefault(n => n.IsKind(SyntaxKind.ThisExpression)).Should().NotBeNull();

            innerJumpBlock.JumpNode.Kind().Should().Be(SyntaxKind.LockStatement);
            innerJumpBlock.Instructions.FirstOrDefault(n => n.IsKind(SyntaxKind.IdentifierName) && n.ToString() == "that").Should().NotBeNull();
        }

        #endregion

        #region Using statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_UsingDeclaration()
        {
            var cfg = Build("using(var stream = new MemoryStream()) { var x = 10; }");
            VerifySimpleJumpBlock(cfg, SyntaxKind.UsingStatement);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_UsingExpression()
        {
            var cfg = Build("using(new MemoryStream()) { var x = 10; }");
            VerifySimpleJumpBlock(cfg, SyntaxKind.UsingStatement);
        }

        #endregion

        #region Fixed statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Fixed()
        {
            var cfg = Build("fixed (int* p = &pt.x) { *p = 1; }");
            VerifySimpleJumpBlock(cfg, SyntaxKind.FixedStatement);
        }

        private static void VerifySimpleJumpBlock(IControlFlowGraph cfg, SyntaxKind kind)
        {
            VerifyCfg(cfg, 3);
            var jumpBlock = cfg.EntryBlock as JumpBlock;
            var bodyBlock = cfg.Blocks.ToList()[1];
            var exitBlock = cfg.ExitBlock;

            jumpBlock.SuccessorBlocks.Should().OnlyContain(bodyBlock);
            bodyBlock.SuccessorBlocks.Should().OnlyContain(exitBlock);

            jumpBlock.JumpNode.Kind().Should().Be(kind);
        }

        #endregion

        #region Checked/unchecked statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Checked()
        {
            var cfg = Build("checked { var i = int.MaxValue + 1; }");
            VerifySimpleJumpBlock(cfg, SyntaxKind.CheckedStatement);

            cfg = Build("unchecked { var i = int.MaxValue + 1; }");
            VerifySimpleJumpBlock(cfg, SyntaxKind.UncheckedStatement);
        }

        #endregion

        #region Unsafe statement

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Unsafe()
        {
            var cfg = Build("unsafe { int* p = &i; *p *= *p; }");
            VerifySimpleJumpBlock(cfg, SyntaxKind.UnsafeStatement);
        }

        #endregion

        #region Logical && and ||

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_LogicalAnd()
        {
            var cfg = Build("var b = a && c;");
            VerifyCfg(cfg, 4);

            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var trueABlock = blocks[1];
            var afterOp = blocks[2];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(trueABlock, afterOp);
            trueABlock.SuccessorBlocks.Should().OnlyContain(afterOp);
            afterOp.SuccessorBlocks.Should().OnlyContain(exitBlock);

            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.LogicalAndExpression);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_LogicalOr()
        {
            var cfg = Build("var b = a || c;");
            VerifyCfg(cfg, 4);

            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var falseABlock = blocks[1];
            var afterOp = blocks[2];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(afterOp, falseABlock);
            falseABlock.SuccessorBlocks.Should().OnlyContain(afterOp);
            afterOp.SuccessorBlocks.Should().OnlyContain(exitBlock);

            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.LogicalOrExpression);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_LogicalAnd_Multiple()
        {
            var cfg = Build("var b = a && c && d;");
            VerifyCfg(cfg, 6);

            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var trueABlock = blocks[1];
            var afterAC = blocks[2] as BinaryBranchBlock;
            var trueACBlock = blocks[3];
            var afterOp = blocks[4];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(trueABlock, afterAC);
            trueABlock.SuccessorBlocks.Should().OnlyContain(afterAC);
            afterAC.SuccessorBlocks.Should().OnlyContainInOrder(trueACBlock, afterOp);
            trueACBlock.SuccessorBlocks.Should().OnlyContain(afterOp);
            afterOp.SuccessorBlocks.Should().OnlyContain(exitBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_LogicalAnd_With_While()
        {
            var cfg = Build("while(a && c) { var x = 10; }");
            VerifyCfg(cfg, 5);

            var aBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var cBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "c"));
            var acBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "a && c")) as BinaryBranchBlock;
            var bodyBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "x = 10"));
            var exitBlock = cfg.ExitBlock;

            aBlock.SuccessorBlocks.Should().OnlyContainInOrder(cBlock, acBlock);
            cBlock.SuccessorBlocks.Should().OnlyContain(acBlock);
            acBlock.SuccessorBlocks.Should().OnlyContainInOrder(bodyBlock, exitBlock);
            bodyBlock.SuccessorBlocks.Should().OnlyContain(aBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_LogicalAnd_With_For()
        {
            var cfg = Build("for(x = 10; a && c; y++) { var z = 11; }");
            VerifyCfg(cfg, 7);

            var initBlock = cfg.EntryBlock;
            var blocks = cfg.Blocks.ToList();
            var aBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "a")) as BinaryBranchBlock;
            var cBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "c"));
            var acBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "a && c")) as BinaryBranchBlock;
            var bodyBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "z = 11"));
            var incrementBlock = blocks
                .First(b => b.Instructions.Any(n => n.ToString() == "y++"));
            var exitBlock = cfg.ExitBlock;

            initBlock.SuccessorBlocks.Should().OnlyContain(aBlock);
            aBlock.SuccessorBlocks.Should().OnlyContainInOrder(cBlock, acBlock);
            cBlock.SuccessorBlocks.Should().OnlyContain(acBlock);
            acBlock.SuccessorBlocks.Should().OnlyContainInOrder(bodyBlock, exitBlock);
            bodyBlock.SuccessorBlocks.Should().OnlyContain(incrementBlock);
            incrementBlock.SuccessorBlocks.Should().OnlyContain(aBlock);
        }

        #endregion

        #region Coalesce expression

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Coalesce()
        {
            var cfg = Build("var a = b ?? c;");
            VerifyCfg(cfg, 4);

            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var bNullBlock = blocks[1];
            var afterOp = blocks[2];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(bNullBlock, afterOp);
            bNullBlock.SuccessorBlocks.Should().OnlyContain(afterOp);
            afterOp.SuccessorBlocks.Should().OnlyContain(exitBlock);

            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.CoalesceExpression);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Coalesce_Multiple()
        {
            var cfg = Build("var a = b ?? c ?? d;");
            VerifyCfg(cfg, 6);

            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var bNullBlock = blocks[1] as BinaryBranchBlock;
            var cNullBlock = blocks[2]; // d
            var cdBlock = blocks[3];    // c ?? d
            var bcdBlock = blocks[4];   // b ?? c ?? d
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(bNullBlock, bcdBlock);
            bNullBlock.SuccessorBlocks.Should().OnlyContainInOrder(cNullBlock, cdBlock);
            cNullBlock.SuccessorBlocks.Should().OnlyContain(cdBlock);
            cdBlock.SuccessorBlocks.Should().OnlyContain(bcdBlock);
            bcdBlock.SuccessorBlocks.Should().OnlyContain(exitBlock);
        }

        #endregion

        #region Conditional expression

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Conditional()
        {
            var cfg = Build("var a = cond ? b : c;");
            VerifyCfg(cfg, 5);

            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var condFalse = blocks[2];
            var condTrue = blocks[1];
            var after = blocks[3];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(condTrue, condFalse);
            condFalse.SuccessorBlocks.Should().OnlyContain(after);
            condTrue.SuccessorBlocks.Should().OnlyContain(after);
            after.SuccessorBlocks.Should().OnlyContain(exitBlock);

            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.ConditionalExpression);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_ConditionalMultiple()
        {
            var cfg = Build("var a = cond1 ? (cond2?x:y) : (cond3?p:q);");
            VerifyCfg(cfg, 11);

            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var cond2Block = blocks[1];
            cond2Block.Instructions.First().ToString().Should().BeEquivalentTo("cond2");
            var cond3Block = blocks[5];
            cond3Block.Instructions.First().ToString().Should().BeEquivalentTo("cond3");


            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(cond2Block, cond3Block);
            cond2Block.SuccessorBlocks.Should().HaveCount(2);
            cond3Block.SuccessorBlocks.Should().HaveCount(2);

            cond2Block
                .SuccessorBlocks.First()
                .SuccessorBlocks.First()
                .SuccessorBlocks.First()
                .SuccessorBlocks.First().ShouldBeEquivalentTo(cfg.ExitBlock);
        }

        #endregion

        #region Conditional access

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_ConditionalAccess()
        {
            var cfg = Build("var a = o?.method();");
            VerifyCfg(cfg, 4);

            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var oNotNull = blocks[1];
            var condAccess = blocks[2];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(condAccess, oNotNull);
            oNotNull.SuccessorBlocks.Should().OnlyContain(condAccess);
            condAccess.SuccessorBlocks.Should().OnlyContain(exitBlock);

            branchBlock.BranchingNode.Kind().Should().Be(SyntaxKind.ConditionalAccessExpression);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_ConditionalAccessNested()
        {
            var cfg = Build("var a = o?.method()?[10];");
            VerifyCfg(cfg, 6);

            var branchBlock = cfg.EntryBlock as BinaryBranchBlock;
            var blocks = cfg.Blocks.ToList();
            var oNotNull = blocks[1] as BinaryBranchBlock;
            var methodNotNull = blocks[2];
            var indexing = blocks[3];
            var whole = blocks[4];
            var exitBlock = cfg.ExitBlock;

            branchBlock.SuccessorBlocks.Should().OnlyContainInOrder(whole, oNotNull);
            oNotNull.SuccessorBlocks.Should().OnlyContainInOrder(indexing, methodNotNull);
            methodNotNull.SuccessorBlocks.Should().OnlyContain(indexing);
            indexing.SuccessorBlocks.Should().OnlyContain(whole);
            whole.SuccessorBlocks.Should().OnlyContain(exitBlock);
        }

        #endregion

        #region Break

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_For_Break()
        {
            var cfg = Build("cw0(); for (a; b && c; d) { if (e) { cw1(); break; } cw2(); } cw3();");
            VerifyCfg(cfg, 10);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0"));
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1"));
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var b = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b")) as BinaryBranchBlock;
            var c = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "c"));
            var bc = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b && c")) as BinaryBranchBlock;

            var d = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "d"));

            var e = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "e")) as BinaryBranchBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContain(b);
            b.SuccessorBlocks.Should().OnlyContainInOrder(c, bc);
            c.SuccessorBlocks.Should().OnlyContain(bc);
            bc.SuccessorBlocks.Should().OnlyContainInOrder(e, cw3);
            e.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(cw3);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
            cw2.SuccessorBlocks.Should().OnlyContain(d);
            d.SuccessorBlocks.Should().OnlyContain(b);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_While_Break()
        {
            var cfg = Build("cw0(); while (b && c) { if (e) { cw1(); break; } cw2(); } cw3();");
            VerifyCfg(cfg, 9);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0"));
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1"));
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var b = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b")) as BinaryBranchBlock;
            var c = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "c"));
            var bc = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b && c")) as BinaryBranchBlock;

            var e = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "e")) as BinaryBranchBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContain(b);
            b.SuccessorBlocks.Should().OnlyContainInOrder(c, bc);
            c.SuccessorBlocks.Should().OnlyContain(bc);
            bc.SuccessorBlocks.Should().OnlyContainInOrder(e, cw3);
            e.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(cw3);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
            cw2.SuccessorBlocks.Should().OnlyContain(b);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Foreach_Break()
        {
            var cfg = Build("cw0(); foreach (var x in xs) { if (e) { cw1(); break; } cw2(); } cw3();");
            VerifyCfg(cfg, 7);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0"));
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1"));
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var xs = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "xs")) as BinaryBranchBlock;

            var e = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "e")) as BinaryBranchBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContain(xs);
            xs.SuccessorBlocks.Should().OnlyContainInOrder(e, cw3);
            e.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(cw3);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
            cw2.SuccessorBlocks.Should().OnlyContain(xs);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Do_Break()
        {
            var cfg = Build("cw0(); do { if (e) { cw1(); break; } cw2(); } while (b && c); cw3();");
            VerifyCfg(cfg, 9);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0"));
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1"));
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var b = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b")) as BinaryBranchBlock;
            var c = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "c"));
            var bc = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b && c")) as BinaryBranchBlock;

            var e = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "e")) as BinaryBranchBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContain(e);
            b.SuccessorBlocks.Should().OnlyContainInOrder(c, bc);
            c.SuccessorBlocks.Should().OnlyContain(bc);
            bc.SuccessorBlocks.Should().OnlyContainInOrder(e, cw3);
            e.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(cw3);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
            cw2.SuccessorBlocks.Should().OnlyContain(b);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Switch_Break()
        {
            var cfg = Build("cw0(); switch(a) { case 1: case 2: cw1(); break; } cw3();");
            VerifyCfg(cfg, 5);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0")) as BranchBlock;
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1")) as JumpBlock;
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var case1 = blocks[1];

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContainInOrder(case1, cw1, cw3);
            case1.SuccessorBlocks.Should().OnlyContain(cw1);
            cw1.SuccessorBlocks.Should().OnlyContain(cw3);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
        }

        #endregion

        #region Continue

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_For_Continue()
        {
            var cfg = Build("cw0(); for (a; b && c; d) { if (e) { cw1(); continue; } cw2(); } cw3();");
            VerifyCfg(cfg, 10);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0"));
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1"));
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var b = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b")) as BinaryBranchBlock;
            var c = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "c"));
            var bc = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b && c")) as BinaryBranchBlock;

            var d = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "d"));

            var e = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "e")) as BinaryBranchBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContain(b);
            b.SuccessorBlocks.Should().OnlyContainInOrder(c, bc);
            c.SuccessorBlocks.Should().OnlyContain(bc);
            bc.SuccessorBlocks.Should().OnlyContainInOrder(e, cw3);
            e.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(d);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
            cw2.SuccessorBlocks.Should().OnlyContain(d);
            d.SuccessorBlocks.Should().OnlyContain(b);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_While_Continue()
        {
            var cfg = Build("cw0(); while (b && c) { if (e) { cw1(); continue; } cw2(); } cw3();");
            VerifyCfg(cfg, 9);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0"));
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1"));
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var b = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b")) as BinaryBranchBlock;
            var c = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "c"));
            var bc = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b && c")) as BinaryBranchBlock;

            var e = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "e")) as BinaryBranchBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContain(b);
            b.SuccessorBlocks.Should().OnlyContainInOrder(c, bc);
            c.SuccessorBlocks.Should().OnlyContain(bc);
            bc.SuccessorBlocks.Should().OnlyContainInOrder(e, cw3);
            e.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(b);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
            cw2.SuccessorBlocks.Should().OnlyContain(b);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Foreach_Continue()
        {
            var cfg = Build("cw0(); foreach (var x in xs) { if (e) { cw1(); continue; } cw2(); } cw3();");
            VerifyCfg(cfg, 7);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0"));
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1"));
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var xs = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "xs")) as BinaryBranchBlock;

            var e = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "e")) as BinaryBranchBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContain(xs);
            xs.SuccessorBlocks.Should().OnlyContainInOrder(e, cw3);
            e.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(xs);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
            cw2.SuccessorBlocks.Should().OnlyContain(xs);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Do_Continue()
        {
            var cfg = Build("cw0(); do { if (e) { cw1(); continue; } cw2(); } while (b && c); cw3();");
            VerifyCfg(cfg, 9);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0"));
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1"));
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var b = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b")) as BinaryBranchBlock;
            var c = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "c"));
            var bc = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "b && c")) as BinaryBranchBlock;

            var e = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "e")) as BinaryBranchBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContain(e);
            b.SuccessorBlocks.Should().OnlyContainInOrder(c, bc);
            c.SuccessorBlocks.Should().OnlyContain(bc);
            bc.SuccessorBlocks.Should().OnlyContainInOrder(e, cw3);
            e.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(b);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
            cw2.SuccessorBlocks.Should().OnlyContain(b);
        }

        #endregion

        #region Switch

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Switch()
        {
            var cfg = Build("cw0(); switch(a) { case 1: case 2: cw1(); break; case 3: default: case 4: cw2(); break; } cw3();");
            VerifyCfg(cfg, 8);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0")) as BranchBlock;
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1")) as JumpBlock;
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2")) as JumpBlock;
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var case3 = blocks[1] as JumpBlock;
            var defaultCase = blocks[2] as JumpBlock;
            var case1 = blocks[4] as JumpBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContainInOrder(case1, cw1, case3, defaultCase, cw2);
            case1.SuccessorBlocks.Should().OnlyContain(cw1);
            case3.SuccessorBlocks.Should().OnlyContain(defaultCase);
            defaultCase.SuccessorBlocks.Should().OnlyContain(cw2);

            cw1.SuccessorBlocks.Should().OnlyContain(cw3);
            cw2.SuccessorBlocks.Should().OnlyContain(cw3);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Switch_NoDefault()
        {
            var cfg = Build("cw0(); switch(a) { case 1: case 2: cw1(); break; case 3: case 4: cw2(); break; } cw3();");
            VerifyCfg(cfg, 7);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0")) as BranchBlock;
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1")) as JumpBlock;
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2")) as JumpBlock;
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var case3 = blocks[1] as JumpBlock;
            var case1 = blocks[3] as JumpBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContainInOrder(case1, cw1, case3, cw2, cw3);
            case1.SuccessorBlocks.Should().OnlyContain(cw1);
            case3.SuccessorBlocks.Should().OnlyContain(cw2);

            cw1.SuccessorBlocks.Should().OnlyContain(cw3);
            cw2.SuccessorBlocks.Should().OnlyContain(cw3);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Switch_GotoCase()
        {
            var cfg = Build("cw0(); switch(a) { case 1: case 2: cw1(); goto case 3; case 3: default: case 4: cw2(); break; } cw3();");
            VerifyCfg(cfg, 8);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0")) as BranchBlock;
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1")) as JumpBlock;
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2")) as JumpBlock;
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var case3 = blocks[1] as JumpBlock;
            var defaultCase = blocks[2] as JumpBlock;
            var case1 = blocks[4] as JumpBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContainInOrder(case1, cw1, case3, defaultCase, cw2);
            case1.SuccessorBlocks.Should().OnlyContain(cw1);
            case3.SuccessorBlocks.Should().OnlyContain(defaultCase);
            defaultCase.SuccessorBlocks.Should().OnlyContain(cw2);

            cw1.SuccessorBlocks.Should().OnlyContain(case3);
            cw2.SuccessorBlocks.Should().OnlyContain(cw3);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Switch_GotoDefault()
        {
            var cfg = Build("cw0(); switch(a) { case 1: case 2: cw1(); goto default; case 3: default: case 4: cw2(); break; } cw3();");
            VerifyCfg(cfg, 8);

            var blocks = cfg.Blocks.ToList();

            var cw0 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw0")) as BranchBlock;
            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1")) as JumpBlock;
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2")) as JumpBlock;
            var cw3 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw3"));

            var case3 = blocks[1] as JumpBlock;
            var defaultCase = blocks[2] as JumpBlock;
            var case1 = blocks[4] as JumpBlock;

            var exitBlock = cfg.ExitBlock;

            cw0.Should().BeSameAs(cfg.EntryBlock);

            cw0.SuccessorBlocks.Should().OnlyContainInOrder(case1, cw1, case3, defaultCase, cw2);
            case1.SuccessorBlocks.Should().OnlyContain(cw1);
            case3.SuccessorBlocks.Should().OnlyContain(defaultCase);
            defaultCase.SuccessorBlocks.Should().OnlyContain(cw2);

            cw1.SuccessorBlocks.Should().OnlyContain(defaultCase);
            cw2.SuccessorBlocks.Should().OnlyContain(cw3);
            cw3.SuccessorBlocks.Should().OnlyContain(exitBlock);
        }

        #endregion

        #region Goto

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Goto_A()
        {
            var cfg = Build("var x = 1; a: b: x++; if (x < 42) { cw1(); goto a; } cw2();");
            VerifyCfg(cfg, 7);

            var blocks = cfg.Blocks.ToList();

            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1")) as JumpBlock;
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));

            var cond = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "x < 42"));

            var a = blocks[1] as JumpBlock;
            var b = blocks[2] as JumpBlock;

            var entry = cfg.EntryBlock;

            (a.JumpNode as LabeledStatementSyntax).Identifier.ValueText.Should().BeEquivalentTo("a");
            (b.JumpNode as LabeledStatementSyntax).Identifier.ValueText.Should().BeEquivalentTo("b");

            entry.SuccessorBlocks.Should().OnlyContain(a);
            a.SuccessorBlocks.Should().OnlyContain(b);
            b.SuccessorBlocks.Should().OnlyContain(cond);
            cond.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(a);
            cw2.SuccessorBlocks.Should().OnlyContain(cfg.ExitBlock);
        }

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_Goto_B()
        {
            var cfg = Build("var x = 1; a: b: x++; if (x < 42) { cw1(); goto b; } cw2();");
            VerifyCfg(cfg, 7);

            var blocks = cfg.Blocks.ToList();

            var cw1 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw1")) as JumpBlock;
            var cw2 = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "cw2"));

            var cond = blocks
                .First(block => block.Instructions.Any(n => n.ToString() == "x < 42"));

            var a = blocks[1] as JumpBlock;
            var b = blocks[2] as JumpBlock;

            var entry = cfg.EntryBlock;

            (a.JumpNode as LabeledStatementSyntax).Identifier.ValueText.Should().BeEquivalentTo("a");
            (b.JumpNode as LabeledStatementSyntax).Identifier.ValueText.Should().BeEquivalentTo("b");

            entry.SuccessorBlocks.Should().OnlyContain(a);
            a.SuccessorBlocks.Should().OnlyContain(b);
            b.SuccessorBlocks.Should().OnlyContain(cond);
            cond.SuccessorBlocks.Should().OnlyContainInOrder(cw1, cw2);
            cw1.SuccessorBlocks.Should().OnlyContain(b);
            cw2.SuccessorBlocks.Should().OnlyContain(cfg.ExitBlock);
        }

        #endregion

        #region Yield return

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_YieldReturn()
        {
            var cfg = Build(@"yield return 5;");
            VerifyMinimalCfg(cfg);
        }

        #endregion

        #region Non-branching expressions

        [TestMethod]
        [TestCategory("CFG")]
        public void Cfg_NonBranchingExpressions()
        {
            var cfg = Build(@"
var x = a < 2; x = a <= 2; x = a > 2; x = a >= 2; x = a == 2; x = a != 2;
var b = x | 2; b = x & 2; b = x ^ 2; b |= 2; b &= false; b ^= 2;
var c = ""c"" + 'c'; c = a - b; c = a * b; c = a / b; c = a % b; c += b; c -= b; c *= b; c /= b; c %= b;
var s = c << 4; s = c >> 4; s <<= 4; s >>= 4;
var p = c++; p = c--; p = ++c; p = --c; p = +c; p = -c; p = !true; p = ~1; p = &c; p = *c;
object o = null; b = (b);");
            VerifyMinimalCfg(cfg);

            cfg = Build(@"
var t = typeof(int); var s = sizeof(int); var v = default(int);
v = checked(1+1); v = unchecked(1+1);");
            VerifyMinimalCfg(cfg);

            cfg = Build("v = (int)1; var o = 1 as object; b = 1 is int;");
            VerifyMinimalCfg(cfg);

            cfg = Build(@"var s = $""Some {text}"";");
            VerifyMinimalCfg(cfg);

            cfg = Build("Method(call, with, arguments);");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = array[1,2,3]; x = array2[1][2];");
            VerifyMinimalCfg(cfg);

            cfg = Build(@"var dict=new Dictionary<string,int>{ [""one""] = 1 };");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = new { Prop1 = 10, Prop2 = 20 };");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = new { Prop1 };");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = new MyClass(5) { Prop1 = 10 };");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = new List<int>{ 10, 20 };");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = new[,] { { 10, 20 }, { 10, 20 } };");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = new int [1,2][3]{ 10, 20 };");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = this.Method(10); var z = x->prop;");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = await this.Method(__arglist(10,11,12));");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = 1; var y = __refvalue(__makeref(x), int); var t = __reftype(__makeref(x));");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = stackalloc int[10];");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = new Action(()=>{}); var y = new Action(i=>{}); var z = new Action(delegate(){});");
            VerifyMinimalCfg(cfg);

            cfg = Build("var x = from t in ts where t > 42;");
            VerifyMinimalCfg(cfg);
        }

        #endregion

        #region Helpers to build the CFG for the tests

        private const string TestInput = @"
namespace NS
{{
  public class Foo
  {{
    public void Bar()
    {{
      {0}
    }}
  }}
}}";

        internal static MethodDeclarationSyntax Compile(string input, string methodName, out SemanticModel semanticModel)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = workspace.CurrentSolution.AddProject("foo", "foo.dll", LanguageNames.CSharp)
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                    .AddDocument("test", input);
                var compilation = document.Project.GetCompilationAsync().Result;
                var tree = compilation.SyntaxTrees.First();

                semanticModel = compilation.GetSemanticModel(tree);

                return tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == methodName);
            }
        }

        private static IControlFlowGraph Build(string methodBody)
        {
            SemanticModel semanticModel;
            var method = Compile(string.Format(TestInput, methodBody), "Bar", out semanticModel);
            return SonarLint.Helpers.FlowAnalysis.CSharp.ControlFlowGraph.Create(method.Body, semanticModel);
        }

        #endregion

        #region Verify helpers

        private static void VerifyCfg(IControlFlowGraph cfg, int numberOfBlocks)
        {
            VerifyBasicCfgProperties(cfg);

            cfg.Blocks.Should().HaveCount(numberOfBlocks);

            if (numberOfBlocks > 1)
            {
                cfg.EntryBlock.Should().NotBeSameAs(cfg.ExitBlock);
                cfg.Blocks.Should().ContainInOrder(new[] { cfg.EntryBlock, cfg.ExitBlock });
            }
            else
            {
                cfg.EntryBlock.Should().BeSameAs(cfg.ExitBlock);
                cfg.Blocks.Should().Contain(cfg.EntryBlock);
            }
        }

        private static void VerifyMinimalCfg(IControlFlowGraph cfg)
        {
            VerifyCfg(cfg, 2);

            cfg.EntryBlock.SuccessorBlocks.Should().OnlyContain(cfg.ExitBlock);
        }

        private static void VerifyEmptyCfg(IControlFlowGraph cfg)
        {
            VerifyCfg(cfg, 1);
        }

        private static void VerifyBasicCfgProperties(IControlFlowGraph cfg)
        {
            cfg.Should().NotBeNull();
            cfg.EntryBlock.Should().NotBeNull();
            cfg.ExitBlock.Should().NotBeNull();

            cfg.ExitBlock.SuccessorBlocks.Should().BeEmpty();
            cfg.ExitBlock.Instructions.Should().BeEmpty();
        }

        #endregion
    }
}
