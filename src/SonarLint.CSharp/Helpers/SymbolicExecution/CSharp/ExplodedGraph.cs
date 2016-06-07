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
using System.Collections.Immutable;

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
        private readonly IEnumerable<IParameterSymbol> declarationParameters = new List<IParameterSymbol>();
        private readonly IEnumerable<IParameterSymbol> nonInDeclarationParameters;
        private readonly Common.LiveVariableAnalysis lva;

        public event EventHandler ExplorationEnded;
        public event EventHandler MaxStepCountReached;
        public event EventHandler<InstructionProcessedEventArgs> InstructionProcessed;
        public event EventHandler ExitBlockReached;
        public event EventHandler<ConditionEvaluatedEventArgs> ConditionEvaluated;

        private readonly Queue<Node> workList = new Queue<Node>();
        private readonly HashSet<Node> nodesAlreadyInGraph = new HashSet<Node>();

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
            }

            var propertySymbol = declaration as IPropertySymbol;
            if (propertySymbol != null)
            {
                declarationParameters = propertySymbol.Parameters;
            }

            nonInDeclarationParameters = declarationParameters.Where(p => p.RefKind != RefKind.None);
        }

        public void Walk()
        {
            var steps = 0;

            EnqueueStartNode();

            while(workList.Any())
            {
                if (steps >= MaxStepCount)
                {
                    OnMaxStepCountReached();
                    return;
                }

                steps++;
                var node = workList.Dequeue();
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
                    VisitSimpleJump(simpleBlock, node);
                    continue;
                }

                var binaryBranchBlock = programPoint.Block as BinaryBranchBlock;
                if (binaryBranchBlock != null)
                {
                    VisitBinaryBranch(binaryBranchBlock, node);
                    continue;
                }

                if (programPoint.Block is BranchBlock)
                {
                    throw new NotImplementedException();
                }
            }

            OnExplorationEnded();
        }

        private void EnqueueStartNode()
        {
            var initialProgramState = new ProgramState();
            foreach (var parameter in declarationParameters)
            {
                initialProgramState = initialProgramState.SetSymbolicValue(parameter, new SymbolicValue());
            }

            EnqueueNewNode(new ProgramPoint(cfg.EntryBlock, 0), initialProgramState);
        }

        #region OnEvent*

        private void OnConditionEvaluated(SyntaxNode branchingNode, bool evaluationValue)
        {
            ConditionEvaluated?.Invoke(this, new ConditionEvaluatedEventArgs
            {
                BranchingNode = branchingNode,
                EvaluationValue = evaluationValue
            });
        }

        private void OnConditionEvaluated(SyntaxNode condition, SyntaxNode branchingNode, bool evaluationValue)
        {
            ConditionEvaluated?.Invoke(this, new ConditionEvaluatedEventArgs
            {
                Condition = condition,
                BranchingNode = branchingNode,
                EvaluationValue = evaluationValue
            });
        }

        private void OnExplorationEnded()
        {
            ExplorationEnded?.Invoke(this, EventArgs.Empty);
        }

        private void OnMaxStepCountReached()
        {
            MaxStepCountReached?.Invoke(this, EventArgs.Empty);
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

        #region Visit*

        private void VisitSimpleJump(SimpleBlock simpleBlock, Node node)
        {
            var newProgramState = GetCleanedProgramState(node);
            EnqueueNewNode(new ProgramPoint(simpleBlock.SuccessorBlock, 0), newProgramState);
        }

        private void VisitBinaryBranch(BinaryBranchBlock binaryBranchBlock, Node node)
        {
            var instruction = binaryBranchBlock.Instructions.LastOrDefault();
            var newProgramState = GetCleanedProgramState(node);

            if (BinaryBranchingKindsWithNoBoolCondition.Contains(binaryBranchBlock.BranchingNode.Kind()))
            {
                EnqueueSuccessors(binaryBranchBlock, newProgramState);
                return;
            }

            if (instruction == null)
            {
                OnConditionEvaluated(binaryBranchBlock.BranchingNode, true);
                OnConditionEvaluated(binaryBranchBlock.BranchingNode, false);
                EnqueueSuccessors(binaryBranchBlock, newProgramState);
                return;
            }

            switch (instruction.Kind())
            {
                case SyntaxKind.IdentifierName:
                    {
                        var identifier = (IdentifierNameSyntax)instruction;
                        var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;

                        if (IsLocalScoped(symbol))
                        {
                            if (node.ProgramState.GetSymbolValue(symbol) == null)
                            {
                                throw new InvalidOperationException("Symbol without symbolic value");
                            }

                            if (node.ProgramState.TrySetSymbolicValue(symbol, SymbolicValue.True, out newProgramState))
                            {
                                OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, true);
                                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.TrueSuccessorBlock), GetCleanedProgramState(newProgramState, node.ProgramPoint.Block));
                            }

                            if (node.ProgramState.TrySetSymbolicValue(symbol, SymbolicValue.False, out newProgramState))
                            {
                                OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, false);
                                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.FalseSuccessorBlock), GetCleanedProgramState(newProgramState, node.ProgramPoint.Block));
                            }
                        }
                        else
                        {
                            OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, true);
                            OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, false);
                            EnqueueSuccessors(binaryBranchBlock, newProgramState);
                        }
                    }
                    break;
                case SyntaxKind.TrueLiteralExpression:
                    OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, true);
                    EnqueueNewNode(new ProgramPoint(binaryBranchBlock.TrueSuccessorBlock), newProgramState);
                    break;
                case SyntaxKind.FalseLiteralExpression:
                    OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, false);
                    EnqueueNewNode(new ProgramPoint(binaryBranchBlock.FalseSuccessorBlock), newProgramState);
                    break;
                default:
                    OnConditionEvaluated(instruction, true);
                    OnConditionEvaluated(instruction, false);
                    EnqueueSuccessors(binaryBranchBlock, newProgramState);
                    break;
            }
        }

        private static readonly ISet<SyntaxKind> BinaryBranchingKindsWithNoBoolCondition = ImmutableHashSet.Create(
            SyntaxKind.ForEachStatement,
            SyntaxKind.CoalesceExpression,
            SyntaxKind.ConditionalAccessExpression);

        private void EnqueueSuccessors(BinaryBranchBlock binaryBranchBlock, ProgramState newProgramState)
        {
            EnqueueNewNode(new ProgramPoint(binaryBranchBlock.TrueSuccessorBlock), newProgramState);
            EnqueueNewNode(new ProgramPoint(binaryBranchBlock.FalseSuccessorBlock), newProgramState);
        }

        private void VisitInstruction(Node node)
        {
            var instruction = node.ProgramPoint.Block.Instructions[node.ProgramPoint.Offset];
            var newProgramPoint = new ProgramPoint(node.ProgramPoint.Block, node.ProgramPoint.Offset + 1);
            var currentState = node.ProgramState;

            switch (instruction.Kind())
            {
                case SyntaxKind.VariableDeclarator:
                    {
                        var declarator = (VariableDeclaratorSyntax)instruction;
                        var leftSymbol = semanticModel.GetDeclaredSymbol(declarator);

                        if (leftSymbol == null)
                        {
                            break;
                        }

                        ISymbol rightSymbol = null;
                        Optional<object> constValue = null;
                        if (declarator.Initializer?.Value != null)
                        {
                            rightSymbol = semanticModel.GetSymbolInfo(declarator.Initializer.Value).Symbol;
                            constValue = semanticModel.GetConstantValue(declarator.Initializer.Value);
                        }

                        currentState = GetNewProgramStateForAssignment(node.ProgramState, leftSymbol, rightSymbol, constValue);
                    }
                    break;
                case SyntaxKind.SimpleAssignmentExpression:
                    {
                        var assignment = (AssignmentExpressionSyntax)instruction;
                        var leftSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;

                        if (IsLocalScoped(leftSymbol))
                        {
                            var rightSymbol = semanticModel.GetSymbolInfo(assignment.Right).Symbol;
                            var constValue = semanticModel.GetConstantValue(assignment.Right);

                            currentState = GetNewProgramStateForAssignment(node.ProgramState, leftSymbol, rightSymbol, constValue);
                        }
                    }
                    break;
                case SyntaxKind.IdentifierName:
                    {
                        var identifier = (IdentifierNameSyntax)instruction;
                        var parenthesized = identifier.GetSelfOrTopParenthesizedExpression();
                        var argument = parenthesized.Parent as ArgumentSyntax;
                        if (argument == null ||
                            argument.RefOrOutKeyword.IsKind(SyntaxKind.None))
                        {
                            break;
                        }

                        var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;

                        if (IsLocalScoped(symbol))
                        {
                            currentState = GetNewProgramStateForAssignment(node.ProgramState, symbol);
                        }
                    }
                    break;
                default:
                    break;
            }

            EnqueueNewNode(newProgramPoint, currentState);
            OnInstructionProcessed(instruction, node.ProgramPoint, currentState);
        }

        private ProgramState GetCleanedProgramState(Node node)
        {
            return GetCleanedProgramState(node.ProgramState, node.ProgramPoint.Block);
        }

        private ProgramState GetCleanedProgramState(ProgramState programState, Block block)
        {
            var liveVariables = lva.GetLiveOut(block)
                .Union(nonInDeclarationParameters); // LVA excludes out and ref parameters
            return programState.CleanAndKeepOnly(liveVariables);
        }

        private bool IsLocalScoped(ISymbol symbol)
        {
            if (symbol == null)
            {
                return false;
            }

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

        private static ProgramState GetNewProgramStateForAssignment(ProgramState programState, ISymbol symbol)
        {
            return GetNewProgramStateForAssignment(programState, symbol, null, new Optional<object>());
        }

        private static ProgramState GetNewProgramStateForAssignment(ProgramState programState, ISymbol leftSymbol, ISymbol rightSymbol,
            Optional<object> constantValue)
        {
            var symbolicValue = programState.GetSymbolValue(rightSymbol);
            if (symbolicValue == null)
            {
                symbolicValue = new SymbolicValue();
            }

            var newProgramState = programState.SetSymbolicValue(leftSymbol, symbolicValue);
            if (constantValue.HasValue)
            {
                var boolConstant = constantValue.Value as bool?;
                if (boolConstant.HasValue)
                {
                    newProgramState = newProgramState.SetSymbolicValue(leftSymbol,
                        boolConstant.Value
                        ? SymbolicValue.True
                        : SymbolicValue.False);
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

            var newNode = new Node(pos, programState);
            if (nodesAlreadyInGraph.Add(newNode))
            {
                workList.Enqueue(newNode);
            }
        }

        private class Node : IEquatable<Node>
        {
            public ProgramState ProgramState { get; }
            public ProgramPoint ProgramPoint { get; }

            public Node(ProgramPoint programPoint, ProgramState programState)
            {
                ProgramState = programState;
                ProgramPoint = programPoint;
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }

                Node n = obj as Node;
                return Equals(n);
            }

            public bool Equals(Node n)
            {
                if (n == null)
                {
                    return false;
                }

                return ProgramState.Equals(n.ProgramState) && ProgramPoint.Equals(n.ProgramPoint);
            }

            public override int GetHashCode()
            {
                var hash = 19;
                hash = hash * 31 + ProgramState.GetHashCode();
                hash = hash * 31 + ProgramPoint.GetHashCode();
                return hash;
            }
        }
    }
}
