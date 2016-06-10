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
using Microsoft.CodeAnalysis.CSharp;
using SonarLint.Helpers.FlowAnalysis.CSharp;
using System.Text;
using SonarLint.Helpers.FlowAnalysis.Common;
using System.Collections.Generic;

namespace SonarLint.UnitTest.Helpers
{
    using LiveVariableAnalysis = SonarLint.Helpers.FlowAnalysis.CSharp.LiveVariableAnalysis;

    [TestClass]
    public class ExplodedGraphTest
    {
        private const string TestInput = @"
namespace NS
{{
  public class Foo
  {{
    public void Bar(bool inParameter, out bool outParameter)
    {{
      {0}
    }}
  }}
}}";

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_SequentialInput()
        {
            string testInput = "var a = true; var b = false; b = !b; a = (b);";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);
            var varDeclarators = method.DescendantNodes().OfType<VariableDeclaratorSyntax>();
            var aSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "a"));
            var bSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "b"));

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);
            var explorationEnded = false;
            explodedGraph.ExplorationEnded += (sender, args) => { explorationEnded = true; };

            var numberOfExitBlockReached = 0;
            explodedGraph.ExitBlockReached += (sender, args) => { numberOfExitBlockReached++; };

            var numberOfProcessedInstructions = 0;
            explodedGraph.InstructionProcessed +=
                (sender, args) =>
                {
                    numberOfProcessedInstructions++;
                    if (args.Instruction.ToString() == "a = true")
                    {
                        Assert.IsTrue(args.ProgramState.GetSymbolValue(aSymbol) == SymbolicValue.True);
                    }
                    if (args.Instruction.ToString() == "b = false")
                    {
                        Assert.IsTrue(args.ProgramState.GetSymbolValue(bSymbol) == SymbolicValue.False);
                    }
                    if (args.Instruction.ToString() == "b = !b")
                    {
                        Assert.IsFalse(args.ProgramState.GetSymbolValue(bSymbol) == SymbolicValue.False);
                        Assert.IsFalse(args.ProgramState.GetSymbolValue(bSymbol) == SymbolicValue.True);
                    }
                    if (args.Instruction.ToString() == "a = (b)")
                    {
                        Assert.AreEqual(
                            args.ProgramState.GetSymbolValue(bSymbol),
                            args.ProgramState.GetSymbolValue(aSymbol));
                    }
                };

            explodedGraph.Walk();

            Assert.IsTrue(explorationEnded);
            Assert.AreEqual(11, numberOfProcessedInstructions);
            Assert.AreEqual(1, numberOfExitBlockReached);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_SequentialInput_OutParameter()
        {
            string testInput = "outParameter = true;";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);
            var parameters = method.DescendantNodes().OfType<ParameterSyntax>();
            var outParameterSymbol = semanticModel.GetDeclaredSymbol(parameters.First(d => d.Identifier.ToString() == "outParameter"));

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);
            var explorationEnded = false;
            explodedGraph.ExplorationEnded += (sender, args) => { explorationEnded = true; };

            var numberOfExitBlockReached = 0;
            explodedGraph.ExitBlockReached += (sender, args) => { numberOfExitBlockReached++; };

            var numberOfProcessedInstructions = 0;
            explodedGraph.InstructionProcessed +=
                (sender, args) =>
                {
                    numberOfProcessedInstructions++;
                    if (args.Instruction.ToString() == "outParameter = true")
                    {
                        Assert.IsTrue(args.ProgramState.GetSymbolValue(outParameterSymbol) == SymbolicValue.True);
                    }
                };

            explodedGraph.Walk();

            Assert.IsTrue(explorationEnded);
            Assert.AreEqual(3, numberOfProcessedInstructions);
            Assert.AreEqual(1, numberOfExitBlockReached);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_SequentialInput_Max()
        {
            var inputBuilder = new StringBuilder();
            for (int i = 0; i < ExplodedGraph.MaxStepCount / 2 + 1; i++)
            {
                inputBuilder.AppendLine($"var x{i} = true;");
            }
            string testInput = inputBuilder.ToString();
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);
            var explorationEnded = false;
            explodedGraph.ExplorationEnded += (sender, args) => { explorationEnded = true; };
            var maxStepCountReached = false;
            explodedGraph.MaxStepCountReached += (sender, args) => { maxStepCountReached = true; };

            var numberOfExitBlockReached = 0;
            explodedGraph.ExitBlockReached += (sender, args) => { numberOfExitBlockReached++; };

            explodedGraph.Walk();

            Assert.IsFalse(explorationEnded);
            Assert.IsTrue(maxStepCountReached);
            Assert.AreEqual(0, numberOfExitBlockReached);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_SingleBranchVisited_If()
        {
            string testInput = "var a = !true; bool b; if (a) { b = true; } else { b = false; } a = b;";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);
            var varDeclarators = method.DescendantNodes().OfType<VariableDeclaratorSyntax>();
            var aSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "a"));
            var bSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "b"));

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);
            var explorationEnded = false;
            explodedGraph.ExplorationEnded += (sender, args) => { explorationEnded = true; };

            var numberOfExitBlockReached = 0;
            explodedGraph.ExitBlockReached += (sender, args) => { numberOfExitBlockReached++; };

            var numberOfLastInstructionVisits = 0;
            var numberOfProcessedInstructions = 0;

            explodedGraph.InstructionProcessed +=
                (sender, args) =>
                {
                    numberOfProcessedInstructions++;
                    if (args.Instruction.ToString() == "a = !true")
                    {
                        Assert.IsTrue(args.ProgramState.GetSymbolValue(aSymbol) == SymbolicValue.False); // Roslyn is clever !true has const value.
                    }
                    if (args.Instruction.ToString() == "b = true")
                    {
                        Assert.Fail("We should never get into this branch");
                    }
                    if (args.Instruction.ToString() == "b = false")
                    {
                        Assert.IsTrue(args.ProgramState.GetSymbolValue(bSymbol) == SymbolicValue.False);
                        Assert.IsNull(args.ProgramState.GetSymbolValue(aSymbol),
                            "a is dead, so there should be no associated value to it.");
                    }
                    if (args.Instruction.ToString() == "a = b")
                    {
                        numberOfLastInstructionVisits++;
                    }
                };

            explodedGraph.Walk();

            Assert.IsTrue(explorationEnded);
            Assert.AreEqual(11, numberOfProcessedInstructions);
            Assert.AreEqual(1, numberOfExitBlockReached);
            Assert.AreEqual(1, numberOfLastInstructionVisits);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_SingleBranchVisited_And()
        {
            string testInput = "var a = !true; if (a && !a) { a = true; }";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);
            var varDeclarators = method.DescendantNodes().OfType<VariableDeclaratorSyntax>();
            var aSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "a"));

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);
            var explorationEnded = false;
            explodedGraph.ExplorationEnded += (sender, args) => { explorationEnded = true; };

            var numberOfExitBlockReached = 0;
            explodedGraph.ExitBlockReached += (sender, args) => { numberOfExitBlockReached++; };

            var numberOfProcessedInstructions = 0;

            explodedGraph.InstructionProcessed +=
                (sender, args) =>
                {
                    numberOfProcessedInstructions++;
                    if (args.Instruction.ToString() == "a = !true")
                    {
                        Assert.IsTrue(args.ProgramState.GetSymbolValue(aSymbol) == SymbolicValue.False); // Roslyn is clever !true has const value.
                    }
                    if (args.Instruction.ToString() == "!a")
                    {
                        Assert.Fail("We should never get into this branch");
                    }
                };

            explodedGraph.Walk();

            Assert.IsTrue(explorationEnded);
            Assert.AreEqual(1, numberOfExitBlockReached);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_BothBranchesVisited()
        {
            string testInput = "var a = !true; bool b; if (inParameter) { b = inParameter; } else { b = !inParameter; } a = b;";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);

            var varDeclarators = method.DescendantNodes().OfType<VariableDeclaratorSyntax>();
            var aSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "a"));
            var bSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "b"));

            var parameters = method.DescendantNodes().OfType<ParameterSyntax>();
            var inParameterSymbol = semanticModel.GetDeclaredSymbol(parameters.First(d => d.Identifier.ToString() == "inParameter"));

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);
            var explorationEnded = false;
            explodedGraph.ExplorationEnded += (sender, args) => { explorationEnded = true; };

            var numberOfExitBlockReached = 0;
            explodedGraph.ExitBlockReached += (sender, args) => { numberOfExitBlockReached++; };

            var numberOfLastInstructionVisits = 0;
            var numberOfProcessedInstructions = 0;

            var visitedBlocks = new HashSet<Block>();
            var branchesVisited = 0;

            explodedGraph.InstructionProcessed +=
                (sender, args) =>
                {
                    visitedBlocks.Add(args.ProgramPoint.Block);

                    numberOfProcessedInstructions++;
                    if (args.Instruction.ToString() == "a = !true")
                    {
                        branchesVisited++;

                        Assert.IsTrue(args.ProgramState.GetSymbolValue(aSymbol) == SymbolicValue.False); // Roslyn is clever !true has const value.
                    }
                    if (args.Instruction.ToString() == "b = inParameter")
                    {
                        branchesVisited++;

                        Assert.IsTrue(args.ProgramState.GetSymbolValue(bSymbol) == SymbolicValue.True);
                        Assert.IsTrue(args.ProgramState.GetSymbolValue(inParameterSymbol) == SymbolicValue.True);
                    }
                    if (args.Instruction.ToString() == "b = !inParameter")
                    {
                        branchesVisited++;

                        // b has value, but not true or false
                        Assert.IsNotNull(args.ProgramState.GetSymbolValue(bSymbol));
                        Assert.IsFalse(args.ProgramState.GetSymbolValue(bSymbol) == SymbolicValue.False);
                        Assert.IsFalse(args.ProgramState.GetSymbolValue(bSymbol) == SymbolicValue.True);

                        Assert.IsTrue(args.ProgramState.GetSymbolValue(inParameterSymbol) == SymbolicValue.False);
                    }
                    if (args.Instruction.ToString() == "a = b")
                    {
                        branchesVisited++;

                        Assert.IsNull(args.ProgramState.GetSymbolValue(inParameterSymbol)); // not out/ref parameter and LVA says dead
                        numberOfLastInstructionVisits++;
                    }
                };

            explodedGraph.Walk();

            Assert.IsTrue(explorationEnded);
            Assert.AreEqual(4 + 1, branchesVisited);
            Assert.AreEqual(1, numberOfExitBlockReached,
                "All variables are dead at the ExitBlock, so whenever we get there, the ExplodedGraph nodes should be the same, " +
                "and thus should be processed only once.");
            Assert.AreEqual(2, numberOfLastInstructionVisits);

            Assert.AreEqual(cfg.Blocks.Count() - 1 /* Exit block */, visitedBlocks.Count);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_BothBranchesVisited_StateMerge()
        {
            string testInput = "var a = !true; bool b; if (inParameter) { b = false; } else { b = false; } a = b;";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);

            var varDeclarators = method.DescendantNodes().OfType<VariableDeclaratorSyntax>();
            var aSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "a"));

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);
            var explorationEnded = false;
            explodedGraph.ExplorationEnded += (sender, args) => { explorationEnded = true; };

            var numberOfExitBlockReached = 0;
            explodedGraph.ExitBlockReached += (sender, args) => { numberOfExitBlockReached++; };

            var numberOfLastInstructionVisits = 0;
            var numberOfProcessedInstructions = 0;

            explodedGraph.InstructionProcessed +=
                (sender, args) =>
                {
                    numberOfProcessedInstructions++;
                    if (args.Instruction.ToString() == "a = b")
                    {
                        Assert.IsTrue(args.ProgramState.GetSymbolValue(aSymbol) == SymbolicValue.False);
                        numberOfLastInstructionVisits++;
                    }
                };

            explodedGraph.Walk();

            Assert.IsTrue(explorationEnded);
            Assert.AreEqual(1, numberOfExitBlockReached);
            Assert.AreEqual(1, numberOfLastInstructionVisits);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_BothBranchesVisited_NonCondition()
        {
            string testInput = "var str = this?.ToString();";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);
            var explorationEnded = false;
            explodedGraph.ExplorationEnded += (sender, args) => { explorationEnded = true; };

            var countConditionEvaluated = 0;
            explodedGraph.ConditionEvaluated += (sender, args) => { countConditionEvaluated++; };

            var visitedBlocks = new HashSet<Block>();

            explodedGraph.InstructionProcessed +=
                (sender, args) =>
                {
                    visitedBlocks.Add(args.ProgramPoint.Block);
                };

            explodedGraph.Walk();

            Assert.IsTrue(explorationEnded);
            Assert.AreEqual(cfg.Blocks.Count() - 1 /* Exit block */, visitedBlocks.Count);
            Assert.AreEqual(0, countConditionEvaluated);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_AllBranchesVisited()
        {
            string testInput = "int i = 1; switch (i) { case 1: default: cw1(); break; case 2: cw2(); break; }";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);
            var explorationEnded = false;
            explodedGraph.ExplorationEnded += (sender, args) => { explorationEnded = true; };

            var numberOfExitBlockReached = 0;
            explodedGraph.ExitBlockReached += (sender, args) => { numberOfExitBlockReached++; };

            var numberOfCw1InstructionVisits = 0;
            var numberOfCw2InstructionVisits = 0;
            var numberOfProcessedInstructions = 0;

            explodedGraph.InstructionProcessed +=
                (sender, args) =>
                {
                    numberOfProcessedInstructions++;
                    if (args.Instruction.ToString() == "cw1()")
                    {
                        numberOfCw1InstructionVisits++;
                    }
                    if (args.Instruction.ToString() == "cw2()")
                    {
                        numberOfCw2InstructionVisits++;
                    }
                };

            explodedGraph.Walk();

            Assert.IsTrue(explorationEnded);
            Assert.AreEqual(1, numberOfExitBlockReached);
            Assert.AreEqual(1, numberOfCw1InstructionVisits);
            Assert.AreEqual(1, numberOfCw2InstructionVisits);
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ExplodedGraph_NonDecisionMakingAssignments()
        {
            string testInput = "var a = true; a |= false; var b = 42; b++;";
            SemanticModel semanticModel;
            var method = ControlFlowGraphTest.Compile(string.Format(TestInput, testInput), "Bar", out semanticModel);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);
            var varDeclarators = method.DescendantNodes().OfType<VariableDeclaratorSyntax>();
            var aSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "a"));
            var bSymbol = semanticModel.GetDeclaredSymbol(varDeclarators.First(d => d.Identifier.ToString() == "b"));

            var cfg = ControlFlowGraph.Create(method.Body, semanticModel);
            var lva = LiveVariableAnalysis.Analyze(cfg, methodSymbol, semanticModel);

            var explodedGraph = new ExplodedGraph(cfg, methodSymbol, semanticModel, lva);

            SymbolicValue sv = null;
            var numberOfProcessedInstructions = 0;
            var branchesVisited = 0;

            explodedGraph.InstructionProcessed +=
                (sender, args) =>
                {
                    numberOfProcessedInstructions++;
                    if (args.Instruction.ToString() == "a = true")
                    {
                        branchesVisited++;
                        Assert.IsTrue(args.ProgramState.GetSymbolValue(aSymbol) == SymbolicValue.True);
                    }
                    if (args.Instruction.ToString() == "a |= false")
                    {
                        branchesVisited++;
                        Assert.IsNotNull(args.ProgramState.GetSymbolValue(aSymbol));
                        Assert.IsFalse(args.ProgramState.GetSymbolValue(aSymbol) == SymbolicValue.False);
                        Assert.IsFalse(args.ProgramState.GetSymbolValue(aSymbol) == SymbolicValue.True);
                    }
                    if (args.Instruction.ToString() == "b = 42")
                    {
                        branchesVisited++;
                        sv = args.ProgramState.GetSymbolValue(bSymbol);
                        Assert.IsNotNull(sv);
                    }
                    if (args.Instruction.ToString() == "b++")
                    {
                        branchesVisited++;
                        var svNew = args.ProgramState.GetSymbolValue(bSymbol);
                        Assert.IsNotNull(svNew);
                        Assert.AreNotEqual(sv, svNew);
                    }
                };

            explodedGraph.Walk();

            Assert.AreEqual(9, numberOfProcessedInstructions);
            Assert.AreEqual(4, branchesVisited);
        }
    }
}
