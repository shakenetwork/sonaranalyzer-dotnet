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
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarLint.Helpers.FlowAnalysis.Common;

namespace SonarLint.Helpers.FlowAnalysis.CSharp
{
    public class ExplodedGraph
    {
        public const int MaxStepCount = 1000;

        private readonly List<Node> nodes = new List<Node>();
        private readonly Dictionary<ProgramPoint, ProgramPoint> programPoints = new Dictionary<ProgramPoint, ProgramPoint>();

        private readonly IControlFlowGraph cfg;
        private readonly SemanticModel semanticModel;
        private readonly ISymbol declaration;
        private readonly IEnumerable<ISymbol> declarationParameters = new List<ISymbol>();
        private readonly Common.LiveVariableAnalysis lva;

        public event EventHandler ExplorationEnded;
        public event EventHandler<InstructionProcessedEventArgs> InstructionProcessed;
        public event EventHandler ExitBlockReached;

        private readonly HashSet<Node> workList = new HashSet<Node>(); // todo: a queue would be better, but we don't want to insert the same node twice

        public ExplodedGraph(IControlFlowGraph cfg, ISymbol declaration, SemanticModel semanticModel, Common.LiveVariableAnalysis lva)
        {
            this.cfg = cfg;
            this.semanticModel = semanticModel;
            this.declaration = declaration;
            this.lva = lva;

            var methodSymbol = declaration as IMethodSymbol;
            if (methodSymbol != null)
            {
                declarationParameters = methodSymbol.Parameters;
                return;
            }

            var propertySymbol = declaration as IPropertySymbol;
            if (propertySymbol != null)
            {
                declarationParameters = propertySymbol.Parameters;
                return;
            }
        }

        public void Walk()
        {
            var steps = 0;

            EnqueueStartNode();

            while(steps < MaxStepCount &&
                workList.Any())
            {
                steps++;
                var node = workList.First();
                workList.Remove(node);
                nodes.Add(node);

                var programPoint = node.ProgramPoint;

                if (programPoint.Block is ExitBlock)
                {
                    OnExitBlockReached();
                    continue;
                }

                if (programPoint.Offset < programPoint.Block.Instructions.Count)
                {
                    VisitInstruction(node);
                    continue;
                }

                var simpleBlock = programPoint.Block as SimpleBlock;
                if (simpleBlock != null)
                {
                    var liveVariables = lva.GetLiveOut(simpleBlock)
                        .Union(declarationParameters); // LVA excludes out and ref parameters
                    var newProgramState = node.ProgramState.CleanAndKeepOnly(liveVariables);
                    EnqueueNewNode(new ProgramPoint(simpleBlock.SuccessorBlock, 0), newProgramState);
                    continue;
                }

                if (programPoint.Block is BinaryBranchBlock)
                {
                    // binary branch
                    continue;
                }

                if (programPoint.Block is BranchBlock)
                {
                    // switch
                    continue;
                }
            }

            OnExplorationEnded();
        }

        private void EnqueueStartNode()
        {
            var initialProgramState = new ProgramState();
            foreach (var parameter in declarationParameters)
            {
                initialProgramState = initialProgramState.AddSymbolValue(parameter, new SymbolicValue());
            }

            EnqueueNewNode(new ProgramPoint(cfg.EntryBlock, 0), initialProgramState);
        }

        #region OnEvent*

        private void OnExplorationEnded()
        {
            ExplorationEnded?.Invoke(this, EventArgs.Empty);
        }

        private void OnExitBlockReached()
        {
            ExitBlockReached?.Invoke(this, EventArgs.Empty);
        }

        private void OnInstructionProcessed(SyntaxNode instruction, ProgramPoint programPoint, ProgramState programState)
        {
            InstructionProcessed?.Invoke(this, new InstructionProcessedEventArgs
            {
                Instruction = instruction,
                ProgramPoint = programPoint,
                ProgramState = programState
            });
        }

        #endregion

        #region Instruction

        private void VisitInstruction(Node node)
        {
            var instruction = node.ProgramPoint.Block.Instructions[node.ProgramPoint.Offset];
            var newProgramPoint = new ProgramPoint(node.ProgramPoint.Block, node.ProgramPoint.Offset + 1);
            var newProgramState = node.ProgramState;

            switch (instruction.Kind())
            {
                case SyntaxKind.VariableDeclarator:
                    {
                        var declarator = (VariableDeclaratorSyntax)instruction;
                        var leftSymbol = semanticModel.GetDeclaredSymbol(declarator);

                        if (leftSymbol == null)
                        {
                            return;
                        }

                        ISymbol rightSymbol = null;
                        Optional<object> constValue = null;
                        if (declarator.Initializer?.Value != null)
                        {
                            rightSymbol = semanticModel.GetSymbolInfo(declarator.Initializer.Value).Symbol;
                            constValue = semanticModel.GetConstantValue(declarator.Initializer.Value);
                        }

                        newProgramState = GetNewProgramStateForAssignment(node.ProgramState, leftSymbol, rightSymbol, constValue);
                    }
                    break;
                case SyntaxKind.SimpleAssignmentExpression:
                    {
                        var assignment = (AssignmentExpressionSyntax)instruction;
                        var leftSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;

                        if (leftSymbol == null ||
                            !IsLocalScoped(leftSymbol))
                        {
                            return;
                        }

                        var rightSymbol = semanticModel.GetSymbolInfo(assignment.Right).Symbol;
                        var constValue = semanticModel.GetConstantValue(assignment.Right);

                        newProgramState = GetNewProgramStateForAssignment(node.ProgramState, leftSymbol, rightSymbol, constValue);
                    }
                    break;
                default:
                    break;
            }

            EnqueueNewNode(newProgramPoint, newProgramState);
            OnInstructionProcessed(instruction, node.ProgramPoint, newProgramState);
        }

        private bool IsLocalScoped(ISymbol symbol)
        {
            var local = symbol as ILocalSymbol;
            if (local == null)
            {
                var parameter = symbol as IParameterSymbol;
                if (parameter == null) // No filter for ref/out
                {
                    return false;
                }
            }

            return symbol.ContainingSymbol != null &&
                symbol.ContainingSymbol.Equals(declaration);
        }

        private static ProgramState GetNewProgramStateForAssignment(ProgramState programState, ISymbol leftSymbol, ISymbol rightSymbol,
            Optional<object> constantValue)
        {
            var symbolicValue = programState.GetSymbolValue(rightSymbol);
            if (symbolicValue == null)
            {
                symbolicValue = new SymbolicValue();
            }

            var newProgramState = programState.AddSymbolValue(leftSymbol, symbolicValue);
            if (constantValue.HasValue)
            {
                var boolConstant = constantValue.Value as bool?;
                if (boolConstant.HasValue)
                {
                    newProgramState = newProgramState.AddSymbolicValueConstraint(symbolicValue,
                        boolConstant.Value
                        ? BooleanLiteralConstraint.True
                        : BooleanLiteralConstraint.False);
                }
            }

            return newProgramState;
        }

        #endregion

        private void EnqueueNewNode(ProgramPoint programPoint, ProgramState programState)
        {
            var pos = programPoint;
            if (programPoints.ContainsKey(programPoint))
            {
                pos = programPoints[programPoint];
            }
            else
            {
                programPoints[pos] = pos;
            }
            workList.Add(new Node(pos, programState));
        }

        private class Node
        {
            public ProgramState ProgramState { get; }
            public ProgramPoint ProgramPoint { get; }

            public Node(ProgramPoint programPoint, ProgramState programState)
            {
                ProgramState = programState;
                ProgramPoint = programPoint;
            }
        }
    }
}
