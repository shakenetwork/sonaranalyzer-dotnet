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
using SonarLint.Rules.CSharp;

namespace SonarLint.Helpers.FlowAnalysis.CSharp
{
    public class ExplodedGraph
    {
        public const int MaxStepCount = 1000;
        private const int MaxProgramPointExecutionCount = 2;

        private readonly List<Node> nodes = new List<Node>();
        private readonly Dictionary<ProgramPoint, ProgramPoint> programPoints = new Dictionary<ProgramPoint, ProgramPoint>();

        private readonly IControlFlowGraph cfg;
        private readonly ISymbol declaration;
        private readonly IEnumerable<IParameterSymbol> declarationParameters = new List<IParameterSymbol>();
        private readonly IEnumerable<IParameterSymbol> nonInDeclarationParameters;
        private readonly Common.LiveVariableAnalysis lva;
        private readonly ICollection<ExplodedGraphCheck> explodedGraphChecks;

        private static readonly ISet<SyntaxKind> BinaryBranchingKindsWithNoBoolCondition = ImmutableHashSet.Create(
            SyntaxKind.ForEachStatement,
            SyntaxKind.CoalesceExpression,
            SyntaxKind.ConditionalAccessExpression);

        internal SemanticModel SemanticModel { get; }

        public event EventHandler ExplorationEnded;
        public event EventHandler MaxStepCountReached;
        public event EventHandler<InstructionProcessedEventArgs> InstructionProcessed;
        public event EventHandler<VisitCountExceedLimitEventArgs> ProgramPointVisitCountExceedLimit;
        public event EventHandler ExitBlockReached;
        public event EventHandler<ConditionEvaluatedEventArgs> ConditionEvaluated;

        private readonly Queue<Node> workList = new Queue<Node>();
        private readonly HashSet<Node> nodesAlreadyInGraph = new HashSet<Node>();

        public ExplodedGraph(IControlFlowGraph cfg, ISymbol declaration, SemanticModel semanticModel, Common.LiveVariableAnalysis lva)
        {
            this.cfg = cfg;
            this.SemanticModel = semanticModel;
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

            explodedGraphChecks = new List<ExplodedGraphCheck>
            {
                // Add mandatory checks
                new NullPointerDereference.NullPointerCheck(this)
            };
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

                var binaryBranchBlock = programPoint.Block as BinaryBranchBlock;
                if (binaryBranchBlock != null)
                {
                    VisitBinaryBranch(binaryBranchBlock, node);
                    continue;
                }

                if (programPoint.Block is BranchBlock ||
                    programPoint.Block is SimpleBlock)
                {
                    var newProgramState = GetCleanedProgramState(node);
                    EnqueueAllSuccessors(programPoint.Block, newProgramState);
                }
            }

            OnExplorationEnded();
        }

        internal void AddExplodedGraphCheck<T>(T check)
            where T : ExplodedGraphCheck
        {
            var matchingCheck = explodedGraphChecks.OfType<T>().SingleOrDefault();
            if (matchingCheck == null)
            {
                explodedGraphChecks.Add(check);
            }
            else
            {
                explodedGraphChecks.Remove(matchingCheck);
                explodedGraphChecks.Add(check);
            }
        }

        #region OnEvent*

        private void OnConditionEvaluated(SyntaxNode branchingNode, bool evaluationValue)
        {
            OnConditionEvaluated(null, branchingNode, evaluationValue);
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

        private void OnProgramPointVisitCountExceedLimit(ProgramPoint programPoint, ProgramState programState)
        {
            ProgramPointVisitCountExceedLimit?.Invoke(this, new VisitCountExceedLimitEventArgs
            {
                Limit = MaxProgramPointExecutionCount,
                ProgramPoint = programPoint,
                ProgramState = programState
            });
        }

        #endregion

        #region Visit*

        private void VisitBinaryBranch(BinaryBranchBlock binaryBranchBlock, Node node)
        {
            var newProgramState = GetCleanedProgramState(node);

            if (BinaryBranchingKindsWithNoBoolCondition.Contains(binaryBranchBlock.BranchingNode.Kind()))
            {
                // Non-bool branching: foreach, ?., ??
                if (binaryBranchBlock.BranchingNode.IsKind(SyntaxKind.ForEachStatement))
                {
                    // foreach variable is not a VariableDeclarator, so we need to assign a value to it
                    var foreachVariableSymbol = SemanticModel.GetDeclaredSymbol(binaryBranchBlock.BranchingNode);
                    newProgramState = SetNewSymbolicValueIfLocal(newProgramState, foreachVariableSymbol);
                }

                EnqueueAllSuccessors(binaryBranchBlock, newProgramState);
                return;
            }

            var instruction = binaryBranchBlock.Instructions.LastOrDefault();
            if (instruction == null)
            {
                // Branching bool conditions, like &&

                OnConditionEvaluated(binaryBranchBlock.BranchingNode, true);
                OnConditionEvaluated(binaryBranchBlock.BranchingNode, false);
                EnqueueAllSuccessors(binaryBranchBlock, newProgramState);
                return;
            }

            switch (instruction.Kind())
            {
                case SyntaxKind.IdentifierName:
                    {
                        // Condition always true/false specific part:

                        var identifier = (IdentifierNameSyntax)instruction;
                        var symbol = SemanticModel.GetSymbolInfo(identifier).Symbol;

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
                            EnqueueAllSuccessors(binaryBranchBlock, newProgramState);
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
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                    {
                        // Null-check specific case, which will be removed when we have proper SV relationship handling

                        var binary = (BinaryExpressionSyntax)instruction;

                        var isLeftNull = binary.Left.RemoveParentheses().IsKind(SyntaxKind.NullLiteralExpression);
                        var isRightNull = binary.Right.RemoveParentheses().IsKind(SyntaxKind.NullLiteralExpression);

                        if (!isRightNull && !isLeftNull)
                        {
                            OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, true);
                            OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, false);
                            EnqueueAllSuccessors(binaryBranchBlock, newProgramState);
                            break;
                        }

                        var identifier = binary.Right.RemoveParentheses() as IdentifierNameSyntax;
                        if (isRightNull)
                        {
                            identifier = binary.Left.RemoveParentheses() as IdentifierNameSyntax;
                        }

                        if (identifier == null)
                        {
                            OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, true);
                            OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, false);
                            EnqueueAllSuccessors(binaryBranchBlock, newProgramState);
                            break;
                        }

                        var symbol = SemanticModel.GetSymbolInfo(identifier).Symbol;
                        if (IsLocalScoped(symbol))
                        {
                            if (node.ProgramState.GetSymbolValue(symbol) == null)
                            {
                                throw new InvalidOperationException("Symbol without symbolic value");
                            }

                            var trueBranchSymbolicValue = SymbolicValue.Null;
                            var falseBranchSymbolicValue = new SymbolicValue(true);

                            if (instruction.IsKind(SyntaxKind.NotEqualsExpression))
                            {
                                falseBranchSymbolicValue = SymbolicValue.Null;
                                trueBranchSymbolicValue = new SymbolicValue(true);
                            }

                            if (node.ProgramState.TrySetSymbolicValue(symbol, trueBranchSymbolicValue, out newProgramState))
                            {
                                OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, true);
                                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.TrueSuccessorBlock), GetCleanedProgramState(newProgramState, node.ProgramPoint.Block));
                            }

                            if (node.ProgramState.TrySetSymbolicValue(symbol, falseBranchSymbolicValue, out newProgramState))
                            {
                                OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, false);
                                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.FalseSuccessorBlock), GetCleanedProgramState(newProgramState, node.ProgramPoint.Block));
                            }
                        }
                        else
                        {
                            OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, true);
                            OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, false);
                            EnqueueAllSuccessors(binaryBranchBlock, newProgramState);
                        }
                    }
                    break;
                default:
                    OnConditionEvaluated(instruction, true);
                    OnConditionEvaluated(instruction, false);
                    EnqueueAllSuccessors(binaryBranchBlock, newProgramState);
                    break;
            }
        }

        private void VisitInstruction(Node node)
        {
            var instruction = node.ProgramPoint.Block.Instructions[node.ProgramPoint.Offset];
            var newProgramPoint = new ProgramPoint(node.ProgramPoint.Block, node.ProgramPoint.Offset + 1);
            var newProgramState = node.ProgramState;

            foreach (var explodedGraphCheck in explodedGraphChecks)
            {
                newProgramState = explodedGraphCheck.ProcessInstruction(node.ProgramPoint, newProgramState);
                if (newProgramState == null)
                {
                    return;
                }
            }

            switch (instruction.Kind())
            {
                case SyntaxKind.VariableDeclarator:
                    {
                        var declarator = (VariableDeclaratorSyntax)instruction;
                        var leftSymbol = SemanticModel.GetDeclaredSymbol(declarator);

                        if (leftSymbol == null)
                        {
                            break;
                        }

                        ISymbol rightSymbol = null;
                        Optional<object> constValue = new object();
                        if (declarator.Initializer?.Value != null)
                        {
                            rightSymbol = SemanticModel.GetSymbolInfo(declarator.Initializer.Value).Symbol;
                            constValue = SemanticModel.GetConstantValue(declarator.Initializer.Value);
                        }

                        newProgramState = GetNewProgramStateForAssignment(node.ProgramState, leftSymbol, rightSymbol, constValue);
                    }
                    break;
                case SyntaxKind.SimpleAssignmentExpression:
                    {
                        var assignment = (AssignmentExpressionSyntax)instruction;
                        var leftSymbol = SemanticModel.GetSymbolInfo(assignment.Left).Symbol;

                        if (IsLocalScoped(leftSymbol))
                        {
                            var rightSymbol = SemanticModel.GetSymbolInfo(assignment.Right).Symbol;
                            var constValue = SemanticModel.GetConstantValue(assignment.Right);

                            newProgramState = GetNewProgramStateForAssignment(node.ProgramState, leftSymbol, rightSymbol, constValue);
                        }
                    }
                    break;
                case SyntaxKind.OrAssignmentExpression:
                case SyntaxKind.AndAssignmentExpression:
                case SyntaxKind.ExclusiveOrAssignmentExpression:

                case SyntaxKind.SubtractAssignmentExpression:
                case SyntaxKind.AddAssignmentExpression:
                case SyntaxKind.DivideAssignmentExpression:
                case SyntaxKind.MultiplyAssignmentExpression:
                case SyntaxKind.ModuloAssignmentExpression:

                case SyntaxKind.LeftShiftAssignmentExpression:
                case SyntaxKind.RightShiftAssignmentExpression:
                    {
                        var assignment = (AssignmentExpressionSyntax)instruction;
                        var leftSymbol = SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
                        newProgramState = SetNewSymbolicValueIfLocal(newProgramState, leftSymbol);
                    }
                    break;

                case SyntaxKind.PreIncrementExpression:
                case SyntaxKind.PreDecrementExpression:
                    {
                        var unary = (PrefixUnaryExpressionSyntax)instruction;
                        var leftSymbol = SemanticModel.GetSymbolInfo(unary.Operand).Symbol;
                        newProgramState = SetNewSymbolicValueIfLocal(newProgramState, leftSymbol);
                    }
                    break;

                case SyntaxKind.PostIncrementExpression:
                case SyntaxKind.PostDecrementExpression:
                    {
                        var unary = (PostfixUnaryExpressionSyntax)instruction;
                        var leftSymbol = SemanticModel.GetSymbolInfo(unary.Operand).Symbol;
                        newProgramState = SetNewSymbolicValueIfLocal(newProgramState, leftSymbol);
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

                        var symbol = SemanticModel.GetSymbolInfo(identifier).Symbol;
                        newProgramState = SetNewSymbolicValueIfLocal(newProgramState, symbol);
                    }
                    break;

                default:
                    break;
            }

            EnqueueNewNode(newProgramPoint, newProgramState);
            OnInstructionProcessed(instruction, node.ProgramPoint, newProgramState);
        }

        private ProgramState SetNewSymbolicValueIfLocal(ProgramState programState, ISymbol symbol)
        {
            var newProgramState = programState;
            if (IsLocalScoped(symbol))
            {
                newProgramState = programState.SetSymbolicValue(symbol, new SymbolicValue(IsNonNullableValueType(symbol)));
            }

            return newProgramState;
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

        internal bool IsLocalScoped(ISymbol symbol)
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

        private static ProgramState GetNewProgramStateForAssignment(ProgramState programState, ISymbol leftSymbol, ISymbol rightSymbol,
            Optional<object> constantValue)
        {
            var symbolicValue = programState.GetSymbolValue(rightSymbol);
            if (symbolicValue == null)
            {
                symbolicValue = new SymbolicValue(IsNonNullableValueType(leftSymbol));
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
                else if (constantValue.Value == null)
                {
                    newProgramState = newProgramState.SetSymbolicValue(
                        leftSymbol,
                        IsNonNullableValueType(leftSymbol) ? new SymbolicValue(true) : SymbolicValue.Null);
                }
            }

            return newProgramState;
        }

        internal static bool IsValueType(ITypeSymbol type)
        {
            return type != null &&
                type.TypeKind == TypeKind.Struct;
        }

        internal static bool IsNonNullableValueType(ISymbol symbol)
        {
            var type = GetTypeOfSymbol(symbol);
            return IsValueType(type) &&
                !type.OriginalDefinition.Is(KnownType.System_Nullable_T);
        }

        internal static ITypeSymbol GetTypeOfSymbol(ISymbol symbol)
        {
            ITypeSymbol type = null;
            var local = symbol as ILocalSymbol;
            if (local != null)
            {
                type = local.Type;
            }

            var parameter = symbol as IParameterSymbol;
            if (parameter != null)
            {
                type = parameter.Type;
            }

            return type;
        }

        #endregion

        #region Enqueue exploded graph node

        private void EnqueueStartNode()
        {
            var initialProgramState = new ProgramState();
            foreach (var parameter in declarationParameters)
            {
                initialProgramState = initialProgramState.SetSymbolicValue(parameter, new SymbolicValue(false));
            }

            EnqueueNewNode(new ProgramPoint(cfg.EntryBlock), initialProgramState);
        }

        private void EnqueueAllSuccessors(Block block, ProgramState newProgramState)
        {
            foreach (var successorBlock in block.SuccessorBlocks)
            {
                EnqueueNewNode(new ProgramPoint(successorBlock), newProgramState);
            }
        }

        private void EnqueueNewNode(ProgramPoint programPoint, ProgramState programState)
        {
            if (programState == null)
            {
                return;
            }

            var pos = programPoint;
            if (programPoints.ContainsKey(programPoint))
            {
                pos = programPoints[programPoint];
            }
            else
            {
                programPoints[pos] = pos;
            }

            if (programState.GetVisitedCount(pos) >= MaxProgramPointExecutionCount)
            {
                OnProgramPointVisitCountExceedLimit(pos, programState);
                return;
            }

            var newNode = new Node(pos, programState.AddVisit(pos));
            if (nodesAlreadyInGraph.Add(newNode))
            {
                workList.Enqueue(newNode);
            }
        }

        #endregion

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

            public bool Equals(Node other)
            {
                if (other == null)
                {
                    return false;
                }

                return ProgramState.Equals(other.ProgramState) && ProgramPoint.Equals(other.ProgramPoint);
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
