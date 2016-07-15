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
    internal class ExplodedGraph
    {
        internal const int MaxStepCount = 1000;
        private const int MaxProgramPointExecutionCount = 2;

        private readonly List<Node> nodes = new List<Node>();
        private readonly Dictionary<ProgramPoint, ProgramPoint> programPoints = new Dictionary<ProgramPoint, ProgramPoint>();

        private readonly IControlFlowGraph cfg;
        private readonly ISymbol declaration;
        private readonly IEnumerable<IParameterSymbol> declarationParameters = new List<IParameterSymbol>();
        private readonly IEnumerable<IParameterSymbol> nonInDeclarationParameters;
        private readonly Common.LiveVariableAnalysis lva;
        private readonly ICollection<ExplodedGraphCheck> explodedGraphChecks;

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
                new NullPointerDereference.NullPointerCheck(this),
                new EmptyNullableValueAccess.NullValueAccessedCheck(this)
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

                if (programPoint.Block is SimpleBlock)
                {
                    var newProgramState = GetCleanedProgramState(node);

                    var jumpBlock = programPoint.Block as JumpBlock;
                    if (jumpBlock != null &&
                        IsValueConsumingStatement(jumpBlock.JumpNode))
                    {
                        newProgramState = newProgramState.PopValue();
                    }

                    EnqueueAllSuccessors(programPoint.Block, newProgramState);
                    continue;
                }

                if (programPoint.Block is BranchBlock)
                {
                    // switch:
                    var newProgramState = node.ProgramState.PopValue();
                    newProgramState = GetCleanedProgramState(newProgramState, node.ProgramPoint.Block);
                    EnqueueAllSuccessors(programPoint.Block, newProgramState);
                }
            }

            OnExplorationEnded();
        }

        private static bool IsValueConsumingStatement(SyntaxNode jumpNode)
        {
            if (jumpNode.IsKind(SyntaxKind.LockStatement))
            {
                return true;
            }

            var usingStatement = jumpNode as UsingStatementSyntax;
            if (usingStatement != null)
            {
                return usingStatement.Expression != null;
            }

            var throwStatement = jumpNode as ThrowStatementSyntax;
            if (throwStatement != null)
            {
                return throwStatement.Expression != null;
            }

            var returnStatement = jumpNode as ReturnStatementSyntax;
            if (returnStatement != null)
            {
                return returnStatement.Expression != null;
            }

            // goto is not putting the expression to the CFG

            return false;
        }

        private static bool ShouldConsumeValue(ExpressionSyntax expression)
        {
            if (expression == null)
            {
                return false;
            }

            var parent = expression.Parent;
            var conditionalAccess = parent as ConditionalAccessExpressionSyntax;
            if (conditionalAccess != null &&
                conditionalAccess.WhenNotNull == expression)
            {
                return ShouldConsumeValue(conditionalAccess.GetSelfOrTopParenthesizedExpression());
            }

            return parent is ExpressionStatementSyntax ||
                parent is YieldStatementSyntax;
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

            switch (binaryBranchBlock.BranchingNode.Kind())
            {
                case SyntaxKind.ForEachStatement:
                    VisitForeachBinaryBranch(binaryBranchBlock, newProgramState);
                    return;
                case SyntaxKind.CoalesceExpression:
                    VisitCoalesceExpressionBinaryBranch(binaryBranchBlock, newProgramState);
                    return;
                case SyntaxKind.ConditionalAccessExpression:
                    VisitConditionalAccessBinaryBranch(binaryBranchBlock, newProgramState);
                    return;
            }

            var instruction = binaryBranchBlock.Instructions.LastOrDefault();
            if (instruction == null)
            {
                // Todo: instead of the null we should pass the condition of the branching node
                VisitBinaryBranch(binaryBranchBlock, node, null);
                return;
            }

            switch (instruction.Kind())
            {
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                    if (TryEnqueueBranchesBasedOn((BinaryExpressionSyntax)instruction, binaryBranchBlock, node))
                    {
                        return;
                    }
                    break;
            }

            VisitBinaryBranch(binaryBranchBlock, node, instruction);
        }

        #region Handle VisitBinaryBranch cases

        private void VisitForeachBinaryBranch(BinaryBranchBlock binaryBranchBlock, ProgramState programState)
        {
            var newProgramState = programState.ClearStack();

            // foreach variable is not a VariableDeclarator, so we need to assign a value to it
            var foreachVariableSymbol = SemanticModel.GetDeclaredSymbol(binaryBranchBlock.BranchingNode);
            newProgramState = SetNewSymbolicValueIfLocal(newProgramState, foreachVariableSymbol, new SymbolicValue());

            EnqueueAllSuccessors(binaryBranchBlock, newProgramState);
        }

        private void VisitCoalesceExpressionBinaryBranch(BinaryBranchBlock binaryBranchBlock, ProgramState programState)
        {
            SymbolicValue sv;
            var ps = programState.PopValue(out sv);

            foreach (var newProgramState in sv.TrySetConstraint(ObjectConstraint.Null, ps))
            {
                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.TrueSuccessorBlock), newProgramState);
            }

            foreach (var newProgramState in sv.TrySetConstraint(ObjectConstraint.NotNull, ps))
            {
                var nps = newProgramState;

                if (!ShouldConsumeValue((BinaryExpressionSyntax)binaryBranchBlock.BranchingNode))
                {
                    nps = nps.PushValue(sv);
                }
                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.FalseSuccessorBlock), nps);
            }
        }

        private void VisitConditionalAccessBinaryBranch(BinaryBranchBlock binaryBranchBlock, ProgramState programState)
        {
            SymbolicValue sv;
            var ps = programState.PopValue(out sv);

            foreach (var newProgramState in sv.TrySetConstraint(ObjectConstraint.Null, ps))
            {
                var nps = newProgramState;

                if (!ShouldConsumeValue((ConditionalAccessExpressionSyntax)binaryBranchBlock.BranchingNode))
                {
                    nps = nps.PushValue(SymbolicValue.Null);
                }
                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.TrueSuccessorBlock), nps);
            }

            foreach (var newProgramState in sv.TrySetConstraint(ObjectConstraint.NotNull, ps))
            {
                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.FalseSuccessorBlock), newProgramState);
            }
        }

        private void VisitBinaryBranch(BinaryBranchBlock binaryBranchBlock, Node node, SyntaxNode instruction)
        {
            var ps = node.ProgramState;

            var forStatement = binaryBranchBlock.BranchingNode as ForStatementSyntax;
            if (forStatement != null &&
                forStatement.Condition == null)
            {
                ps = ps.PushValue(SymbolicValue.True);
            }

            SymbolicValue sv;
            ps = ps.PopValue(out sv);
            ps = ClearStackForForLoop(binaryBranchBlock.BranchingNode as ForStatementSyntax, ps);

            foreach (var newProgramState in sv.TrySetConstraint(BoolConstraint.True, ps))
            {
                OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, evaluationValue: true);

                var nps = FixStackForLogicalOr(binaryBranchBlock, newProgramState);
                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.TrueSuccessorBlock), GetCleanedProgramState(nps, node.ProgramPoint.Block));
            }

            foreach (var newProgramState in sv.TrySetConstraint(BoolConstraint.False, ps))
            {
                OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, evaluationValue: false);

                var nps = FixStackForLogicalAnd(binaryBranchBlock, newProgramState);
                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.FalseSuccessorBlock), GetCleanedProgramState(nps, node.ProgramPoint.Block));
            }
        }

        private bool TryEnqueueBranchesBasedOn(BinaryExpressionSyntax instruction, BinaryBranchBlock binaryBranchBlock, Node node)
        {
            var identifier = GetNullComparedIdentifier(instruction);
            if (identifier == null)
            {
                return false;
            }

            var symbol = SemanticModel.GetSymbolInfo(identifier).Symbol;
            if (!IsLocalScoped(symbol))
            {
                return false;
            }

            var symbolicValue = node.ProgramState.GetSymbolValue(symbol);
            if (symbolicValue == null)
            {
                throw new InvalidOperationException("Symbol without symbolic value");
            }

            var trueBranchConstraint = ObjectConstraint.Null;
            var falseBranchConstraint = ObjectConstraint.NotNull;

            if (instruction.IsKind(SyntaxKind.NotEqualsExpression))
            {
                falseBranchConstraint = ObjectConstraint.Null;
                trueBranchConstraint = ObjectConstraint.NotNull;
            }

            ProgramState programState = node.ProgramState;
            programState = programState.PopValue();
            programState = ClearStackForForLoop(binaryBranchBlock.BranchingNode as ForStatementSyntax, programState);

            foreach (var newProgramState in symbolicValue.TrySetConstraint(trueBranchConstraint, programState))
            {
                OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, evaluationValue: true);

                var nps = FixStackForLogicalOr(binaryBranchBlock, newProgramState);
                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.TrueSuccessorBlock), GetCleanedProgramState(nps, node.ProgramPoint.Block));
            }

            foreach (var newProgramState in symbolicValue.TrySetConstraint(falseBranchConstraint, programState))
            {
                OnConditionEvaluated(instruction, binaryBranchBlock.BranchingNode, evaluationValue: false);

                var nps = FixStackForLogicalAnd(binaryBranchBlock, newProgramState);
                EnqueueNewNode(new ProgramPoint(binaryBranchBlock.FalseSuccessorBlock), GetCleanedProgramState(nps, node.ProgramPoint.Block));
            }

            return true;
        }

        private static ProgramState ClearStackForForLoop(ForStatementSyntax forStatement, ProgramState programState)
        {
            return forStatement == null
                ? programState
                : programState.ClearStack();
        }

        private static IdentifierNameSyntax GetNullComparedIdentifier(BinaryExpressionSyntax instruction)
        {
            var left = instruction.Left.RemoveParentheses();
            var right = instruction.Right.RemoveParentheses();
            var isLeftNull = left.IsKind(SyntaxKind.NullLiteralExpression);
            var isRightNull = right.IsKind(SyntaxKind.NullLiteralExpression);

            if (!isRightNull && !isLeftNull)
            {
                return null;
            }

            var identifier = right as IdentifierNameSyntax;
            if (isRightNull)
            {
                identifier = left as IdentifierNameSyntax;
            }

            return identifier;
        }

        private static ProgramState FixStackForLogicalAnd(BinaryBranchBlock binaryBranchBlock, ProgramState newProgramState)
        {
            return binaryBranchBlock.BranchingNode.IsKind(SyntaxKind.LogicalAndExpression)
                ? newProgramState.PushValue(SymbolicValue.False)
                : newProgramState;
        }

        private static ProgramState FixStackForLogicalOr(BinaryBranchBlock binaryBranchBlock, ProgramState newProgramState)
        {
            return binaryBranchBlock.BranchingNode.IsKind(SyntaxKind.LogicalOrExpression)
                ? newProgramState.PushValue(SymbolicValue.True)
                : newProgramState;
        }

        #endregion

        private void VisitInstruction(Node node)
        {
            var instruction = node.ProgramPoint.Block.Instructions[node.ProgramPoint.Offset];
            var expression = instruction as ExpressionSyntax;
            var parenthesizedExpression = expression?.GetSelfOrTopParenthesizedExpression();
            var newProgramPoint = new ProgramPoint(node.ProgramPoint.Block, node.ProgramPoint.Offset + 1);
            var newProgramState = node.ProgramState;

            foreach (var explodedGraphCheck in explodedGraphChecks)
            {
                newProgramState = explodedGraphCheck.PreProcessInstruction(node.ProgramPoint, newProgramState);
                if (newProgramState == null)
                {
                    return;
                }
            }

            switch (instruction.Kind())
            {
                case SyntaxKind.VariableDeclarator:
                    newProgramState = VisitVariableDeclarator((VariableDeclaratorSyntax)instruction, newProgramState);
                    break;
                case SyntaxKind.SimpleAssignmentExpression:
                    newProgramState = VisitSimpleAssignment((AssignmentExpressionSyntax)instruction, newProgramState);
                    break;

                case SyntaxKind.OrAssignmentExpression:
                    newProgramState = VisitBooleanBinaryOpAssignment(newProgramState, (AssignmentExpressionSyntax)instruction, (l, r) => new OrSymbolicValue(l, r));
                    break;
                case SyntaxKind.AndAssignmentExpression:
                    newProgramState = VisitBooleanBinaryOpAssignment(newProgramState, (AssignmentExpressionSyntax)instruction, (l, r) => new AndSymbolicValue(l, r));
                    break;
                case SyntaxKind.ExclusiveOrAssignmentExpression:
                    newProgramState = VisitBooleanBinaryOpAssignment(newProgramState, (AssignmentExpressionSyntax)instruction, (l, r) => new XorSymbolicValue(l, r));
                    break;

                case SyntaxKind.SubtractAssignmentExpression:
                case SyntaxKind.AddAssignmentExpression:
                case SyntaxKind.DivideAssignmentExpression:
                case SyntaxKind.MultiplyAssignmentExpression:
                case SyntaxKind.ModuloAssignmentExpression:

                case SyntaxKind.LeftShiftAssignmentExpression:
                case SyntaxKind.RightShiftAssignmentExpression:
                    newProgramState = VisitOpAssignment((AssignmentExpressionSyntax)instruction, newProgramState);
                    break;

                case SyntaxKind.PreIncrementExpression:
                case SyntaxKind.PreDecrementExpression:
                    newProgramState = VisitPrefixIncrement((PrefixUnaryExpressionSyntax)instruction, newProgramState);
                    break;

                case SyntaxKind.PostIncrementExpression:
                case SyntaxKind.PostDecrementExpression:
                    newProgramState = VisitPostfixIncrement((PostfixUnaryExpressionSyntax)instruction, newProgramState);
                    break;

                case SyntaxKind.IdentifierName:
                    newProgramState = VisitIdentifier((IdentifierNameSyntax)instruction, newProgramState);
                    break;

                case SyntaxKind.BitwiseOrExpression:
                    newProgramState = VisitBooleanBinaryOperator(newProgramState, (l, r) => new OrSymbolicValue(l, r));
                    break;
                case SyntaxKind.BitwiseAndExpression:
                    newProgramState = VisitBooleanBinaryOperator(newProgramState, (l, r) => new AndSymbolicValue(l, r));
                    break;
                case SyntaxKind.ExclusiveOrExpression:
                    newProgramState = VisitBooleanBinaryOperator(newProgramState, (l, r) => new XorSymbolicValue(l, r));
                    break;

                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:

                case SyntaxKind.SubtractExpression:
                case SyntaxKind.AddExpression:
                case SyntaxKind.DivideExpression:
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.ModuloExpression:

                case SyntaxKind.LeftShiftExpression:
                case SyntaxKind.RightShiftExpression:
                    newProgramState = newProgramState.PopValues(2);
                    newProgramState = newProgramState.PushValue(new SymbolicValue());
                    break;

                case SyntaxKind.BitwiseNotExpression:
                case SyntaxKind.UnaryMinusExpression:
                case SyntaxKind.UnaryPlusExpression:
                case SyntaxKind.AddressOfExpression:
                case SyntaxKind.PointerIndirectionExpression:

                case SyntaxKind.PointerMemberAccessExpression:

                case SyntaxKind.MakeRefExpression:
                case SyntaxKind.RefTypeExpression:
                case SyntaxKind.RefValueExpression:

                case SyntaxKind.AsExpression:
                case SyntaxKind.IsExpression:
                case SyntaxKind.CastExpression:

                case SyntaxKind.MemberBindingExpression:
                    newProgramState = newProgramState.PopValue();
                    newProgramState = newProgramState.PushValue(new SymbolicValue());
                    break;

                case SyntaxKind.SimpleMemberAccessExpression:
                    {
                        var check = explodedGraphChecks.OfType<EmptyNullableValueAccess.NullValueAccessedCheck>().FirstOrDefault();
                        if (check == null ||
                            !check.TryProcessInstruction((MemberAccessExpressionSyntax)instruction, newProgramState, out newProgramState))
                        {
                            // Default behavior
                            newProgramState = newProgramState.PopValue();
                            newProgramState = newProgramState.PushValue(new SymbolicValue());
                        }
                    }
                    break;

                case SyntaxKind.GenericName:
                case SyntaxKind.AliasQualifiedName:
                case SyntaxKind.QualifiedName:

                case SyntaxKind.PredefinedType:
                case SyntaxKind.NullableType:

                case SyntaxKind.OmittedArraySizeExpression:

                case SyntaxKind.AnonymousMethodExpression:
                case SyntaxKind.ParenthesizedLambdaExpression:
                case SyntaxKind.SimpleLambdaExpression:
                case SyntaxKind.QueryExpression:

                case SyntaxKind.ArgListExpression:
                    newProgramState = newProgramState.PushValue(new SymbolicValue());
                    break;
                case SyntaxKind.LogicalNotExpression:
                    {
                        SymbolicValue sv;
                        newProgramState = newProgramState.PopValue(out sv);
                        newProgramState = newProgramState.PushValue(new LogicalNotSymbolicValue(sv));
                    }
                    break;

                case SyntaxKind.TrueLiteralExpression:
                    newProgramState = newProgramState.PushValue(SymbolicValue.True);
                    break;
                case SyntaxKind.FalseLiteralExpression:
                    newProgramState = newProgramState.PushValue(SymbolicValue.False);
                    break;
                case SyntaxKind.NullLiteralExpression:
                    newProgramState = newProgramState.PushValue(SymbolicValue.Null);
                    break;
                case SyntaxKind.CharacterLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.NumericLiteralExpression:

                case SyntaxKind.ThisExpression:
                case SyntaxKind.BaseExpression:

                case SyntaxKind.DefaultExpression:
                case SyntaxKind.SizeOfExpression:
                case SyntaxKind.TypeOfExpression:

                case SyntaxKind.ArrayCreationExpression:
                case SyntaxKind.ImplicitArrayCreationExpression:
                case SyntaxKind.StackAllocArrayCreationExpression:
                    {
                        var sv = new SymbolicValue();
                        newProgramState = sv.SetConstraint(ObjectConstraint.NotNull, newProgramState);
                        newProgramState = newProgramState.PushValue(sv);
                    }
                    break;

                case SyntaxKind.AnonymousObjectCreationExpression:
                    {
                        var creation = (AnonymousObjectCreationExpressionSyntax)instruction;
                        newProgramState = newProgramState.PopValues(creation.Initializers.Count);

                        var sv = new SymbolicValue();
                        newProgramState = sv.SetConstraint(ObjectConstraint.NotNull, newProgramState);
                        newProgramState = newProgramState.PushValue(sv);
                    }
                    break;

                case SyntaxKind.AwaitExpression:
                case SyntaxKind.CheckedExpression:
                case SyntaxKind.UncheckedExpression:
                    // Do nothing
                    break;

                case SyntaxKind.InterpolatedStringExpression:
                    newProgramState = newProgramState.PopValues(((InterpolatedStringExpressionSyntax)instruction).Contents.OfType<InterpolationSyntax>().Count());
                    newProgramState = newProgramState.PushValue(new SymbolicValue());
                    break;

                case SyntaxKind.InvocationExpression:
                    newProgramState = newProgramState.PopValues((((InvocationExpressionSyntax)instruction).ArgumentList?.Arguments.Count ?? 0) + 1);
                    newProgramState = newProgramState.PushValue(new SymbolicValue());
                    break;

                case SyntaxKind.ObjectCreationExpression:
                    newProgramState = VisitObjectCreation((ObjectCreationExpressionSyntax)instruction, newProgramState);
                    break;

                case SyntaxKind.ElementAccessExpression:
                    newProgramState = newProgramState.PopValues((((ElementAccessExpressionSyntax)instruction).ArgumentList?.Arguments.Count ?? 0) + 1);
                    newProgramState = newProgramState.PushValue(new SymbolicValue());
                    break;

                case SyntaxKind.ImplicitElementAccess:
                    newProgramState = newProgramState.PopValues(((ImplicitElementAccessSyntax)instruction).ArgumentList?.Arguments.Count ?? 0);
                    break;

                case SyntaxKind.ObjectInitializerExpression:
                case SyntaxKind.ArrayInitializerExpression:
                case SyntaxKind.CollectionInitializerExpression:
                case SyntaxKind.ComplexElementInitializerExpression:
                    newProgramState = VisitInitializer(instruction, parenthesizedExpression, newProgramState);
                    break;

                case SyntaxKind.ArrayType:
                    newProgramState = newProgramState.PopValues(((ArrayTypeSyntax)instruction).RankSpecifiers.SelectMany(rs => rs.Sizes).Count());
                    break;

                case SyntaxKind.ElementBindingExpression:
                    newProgramState = newProgramState.PopValues(((ElementBindingExpressionSyntax)instruction).ArgumentList?.Arguments.Count ?? 0);
                    newProgramState = newProgramState.PushValue(new SymbolicValue());
                    break;

                default:
                    throw new NotImplementedException($"{instruction.Kind()}");
            }

            if (ShouldConsumeValue(parenthesizedExpression))
            {
                newProgramState = newProgramState.PopValue();
                System.Diagnostics.Debug.Assert(!newProgramState.HasValue);
            }

            EnqueueNewNode(newProgramPoint, newProgramState);
            OnInstructionProcessed(instruction, node.ProgramPoint, newProgramState);
        }

        #region VisitExpression*

        private static ProgramState VisitBooleanBinaryOperator(ProgramState programState, Func<SymbolicValue, SymbolicValue, SymbolicValue> symbolicValueFactory)
        {
            SymbolicValue leftSymbol;
            SymbolicValue rightSymbol;
            var newProgramState = programState.PopValue(out leftSymbol);
            newProgramState = newProgramState.PopValue(out rightSymbol);
            return newProgramState.PushValue(symbolicValueFactory(leftSymbol, rightSymbol));
        }

        private ProgramState VisitBooleanBinaryOpAssignment(ProgramState programState, AssignmentExpressionSyntax assignment,
            Func<SymbolicValue, SymbolicValue, SymbolicValue> symbolicValueFactory)
        {
            var symbol = SemanticModel.GetSymbolInfo(assignment.Left).Symbol;

            SymbolicValue leftSymbol;
            SymbolicValue rightSymbol;
            var newProgramState = programState.PopValue(out leftSymbol);
            newProgramState = newProgramState.PopValue(out rightSymbol);

            var sv = symbolicValueFactory(leftSymbol, rightSymbol);
            newProgramState = newProgramState.PushValue(sv);
            return SetNewSymbolicValueIfLocal(newProgramState, symbol, sv);
        }

        private ProgramState VisitObjectCreation(ObjectCreationExpressionSyntax ctor, ProgramState programState)
        {
            var newProgramState = programState.PopValues(ctor.ArgumentList?.Arguments.Count ?? 0);
            var sv = new SymbolicValue();

            var ctorSymbol = SemanticModel.GetSymbolInfo(ctor).Symbol as IMethodSymbol;
            if (ctorSymbol == null)
            {
                // Add no constraint
            }
            else if (IsEmptyNullableCtorCall(ctorSymbol))
            {
                newProgramState = sv.SetConstraint(ObjectConstraint.Null, newProgramState);
            }
            else
            {
                newProgramState = sv.SetConstraint(ObjectConstraint.NotNull, newProgramState);
            }

            return newProgramState.PushValue(sv);
        }

        private static ProgramState VisitInitializer(SyntaxNode instruction, ExpressionSyntax parenthesizedExpression, ProgramState programState)
        {
            var init = (InitializerExpressionSyntax)instruction;
            var newProgramState = programState.PopValues(init.Expressions.Count);

            if (!(parenthesizedExpression.Parent is ObjectCreationExpressionSyntax) &&
                !(parenthesizedExpression.Parent is ArrayCreationExpressionSyntax) &&
                !(parenthesizedExpression.Parent is AnonymousObjectCreationExpressionSyntax) &&
                !(parenthesizedExpression.Parent is ImplicitArrayCreationExpressionSyntax))
            {
                newProgramState = newProgramState.PushValue(new SymbolicValue());
            }

            return newProgramState;
        }

        private ProgramState VisitIdentifier(IdentifierNameSyntax identifier, ProgramState programState)
        {
            var symbol = SemanticModel.GetSymbolInfo(identifier).Symbol;
            var sv = programState.GetSymbolValue(symbol);
            if (sv == null)
            {
                sv = new SymbolicValue();
            }
            var newProgramState = programState.PushValue(sv);

            var parenthesized = identifier.GetSelfOrTopParenthesizedExpression();
            var argument = parenthesized.Parent as ArgumentSyntax;
            if (argument == null ||
                argument.RefOrOutKeyword.IsKind(SyntaxKind.None))
            {
                return newProgramState;
            }

            newProgramState = newProgramState.PopValue();
            sv = new SymbolicValue();
            newProgramState = newProgramState.PushValue(sv);
            return SetNewSymbolicValueIfLocal(newProgramState, symbol, sv);
        }

        private ProgramState VisitPostfixIncrement(PostfixUnaryExpressionSyntax unary, ProgramState newProgramState)
        {
            var leftSymbol = SemanticModel.GetSymbolInfo(unary.Operand).Symbol;

            // Do not change the stacked value
            var sv = new SymbolicValue();
            return SetNewSymbolicValueIfLocal(newProgramState, leftSymbol, sv);
        }

        private ProgramState VisitPrefixIncrement(PrefixUnaryExpressionSyntax unary, ProgramState programState)
        {
            var newProgramState = programState;
            var leftSymbol = SemanticModel.GetSymbolInfo(unary.Operand).Symbol;
            newProgramState = newProgramState.PopValue();

            var sv = new SymbolicValue();
            newProgramState = newProgramState.PushValue(sv);
            return SetNewSymbolicValueIfLocal(newProgramState, leftSymbol, sv);
        }

        private ProgramState VisitOpAssignment(AssignmentExpressionSyntax assignment, ProgramState programState)
        {
            var newProgramState = programState;
            var leftSymbol = SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
            newProgramState = newProgramState.PopValues(2);

            var sv = new SymbolicValue();
            newProgramState = newProgramState.PushValue(sv);
            newProgramState = SetNewSymbolicValueIfLocal(newProgramState, leftSymbol, sv);
            return newProgramState;
        }

        private ProgramState VisitSimpleAssignment(AssignmentExpressionSyntax assignment, ProgramState programState)
        {
            var newProgramState = programState;
            SymbolicValue sv;
            newProgramState = newProgramState.PopValue(out sv);
            newProgramState = newProgramState.PopValue();

            var leftSymbol = SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
            if (leftSymbol != null &&
                IsLocalScoped(leftSymbol))
            {
                newProgramState = newProgramState.SetSymbolicValue(leftSymbol, sv);
            }

            return newProgramState.PushValue(sv);
        }

        private ProgramState VisitVariableDeclarator(VariableDeclaratorSyntax declarator, ProgramState programState)
        {
            var newProgramState = programState;

            var sv = new SymbolicValue();
            if (declarator.Initializer?.Value != null)
            {
                newProgramState = newProgramState.PopValue(out sv);
            }

            var leftSymbol = SemanticModel.GetDeclaredSymbol(declarator);
            if (leftSymbol != null &&
                 IsLocalScoped(leftSymbol))
            {
                newProgramState = newProgramState.SetSymbolicValue(leftSymbol, sv);
            }

            return newProgramState;
        }

        #endregion

        private ProgramState SetNewSymbolicValueIfLocal(ProgramState programState, ISymbol symbol, SymbolicValue symbolicValue)
        {
            return IsLocalScoped(symbol)
                ? SetNewSymbolicValue(programState, symbol, symbolicValue, IsNonNullableValueType(symbol))
                : programState;
        }

        private static ProgramState SetNewSymbolicValue(ProgramState programState, ISymbol symbol, SymbolicValue value, bool shouldSetNotNullConstraint)
        {
            var newProgramState = programState.SetSymbolicValue(symbol, value);
            if (shouldSetNotNullConstraint)
            {
                newProgramState = value.SetConstraint(ObjectConstraint.NotNull, newProgramState);
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
            return programState.Clean(liveVariables);
        }

        internal bool IsLocalScoped(ISymbol symbol)
        {
            if (symbol == null ||
                lva.CapturedVariables.Contains(symbol)) // Captured variables are not locally scoped, they are compiled to class fields
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

        internal bool IsNullableLocalScoped(ISymbol symbol)
        {
            var type = GetTypeOfSymbol(symbol);
            return type != null &&
                type.OriginalDefinition.Is(KnownType.System_Nullable_T) &&
                IsLocalScoped(symbol);
        }

        private static bool IsEmptyNullableCtorCall(IMethodSymbol nullableConstructorCall)
        {
            return nullableConstructorCall != null &&
                nullableConstructorCall.MethodKind == MethodKind.Constructor &&
                nullableConstructorCall.ReceiverType.OriginalDefinition.Is(KnownType.System_Nullable_T) &&
                nullableConstructorCall.Parameters.Length == 0;
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
                initialProgramState = initialProgramState.SetSymbolicValue(parameter, new SymbolicValue());
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
