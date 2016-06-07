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

namespace SonarLint.UnitTest.Helpers
{
    [TestClass]
    public class ExplodedGraphTest
    {
        private const string TestInput = @"
namespace NS
{{
  public class Foo
  {{
    public void Bar(out bool outParameter)
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
                        bool value;
                        Assert.IsTrue(args.ProgramState.TryGetBoolValue(aSymbol, out value) && value);
                    }
                    if (args.Instruction.ToString() == "b = false")
                    {
                        bool value;
                        Assert.IsTrue(args.ProgramState.TryGetBoolValue(bSymbol, out value) && !value);
                    }
                    if (args.Instruction.ToString() == "b = !b")
                    {
                        bool value;
                        Assert.IsFalse(args.ProgramState.TryGetBoolValue(bSymbol, out value));
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
                        bool value;
                        Assert.IsTrue(args.ProgramState.TryGetBoolValue(outParameterSymbol, out value) && value);
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

            var numberOfExitBlockReached = 0;
            explodedGraph.ExitBlockReached += (sender, args) => { numberOfExitBlockReached++; };

            explodedGraph.Walk();

            Assert.IsTrue(explorationEnded);
            Assert.AreEqual(0, numberOfExitBlockReached);
        }
    }
}
