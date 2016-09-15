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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Helpers.FlowAnalysis.CSharp;
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using Microsoft.CodeAnalysis.Text;

namespace SonarAnalyzer.Rules.CSharp
{
    using LiveVariableAnalysis = Helpers.FlowAnalysis.CSharp.LiveVariableAnalysis;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious, Tag.Cert, Tag.Cwe, Tag.Unused)]
    public class DeadStores : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1854";
        internal const string Title = "Dead stores should be removed";
        internal const string Description =
            "A dead store happens when a local variable is assigned a value that is not read by " +
            "any subsequent instruction. Calculating or retrieving a value only to then overwrite " +
            "it or throw it away, could indicate a serious error in the code.Even if it's not an " +
            "error, it is at best a waste of resources. Therefore all calculated values should be " +
            "used.";
        internal const string MessageFormat = "Remove this useless assignment to local variable \"{0}\".";
        internal const string Category = SonarAnalyzer.Common.Category.Performance;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (BaseMethodDeclarationSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(declaration);
                    if (symbol == null)
                    {
                        return;
                    }

                    CheckForDeadStores(declaration.Body, symbol, c);
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
                    var symbol = c.SemanticModel.GetDeclaredSymbol(declaration);
                    if (symbol == null)
                    {
                        return;
                    }

                    CheckForDeadStores(declaration.Body, symbol, c);
                },
                SyntaxKind.GetAccessorDeclaration,
                SyntaxKind.SetAccessorDeclaration,
                SyntaxKind.AddAccessorDeclaration,
                SyntaxKind.RemoveAccessorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (AnonymousFunctionExpressionSyntax)c.Node;
                    var symbol = c.SemanticModel.GetSymbolInfo(declaration).Symbol;
                    if (symbol == null)
                    {
                        return;
                    }

                    CheckForDeadStores(declaration.Body, symbol, c);
                },
                SyntaxKind.AnonymousMethodExpression,
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression);
        }

        private static void CheckForDeadStores(CSharpSyntaxNode node, ISymbol declaration, SyntaxNodeAnalysisContext context)
        {
            IControlFlowGraph cfg;
            if (!ControlFlowGraph.TryGet(node, context.SemanticModel, out cfg))
            {
                return;
            }

            var lva = LiveVariableAnalysis.Analyze(cfg, declaration, context.SemanticModel);

            foreach (var block in cfg.Blocks)
            {
                CheckCfgBlockForDeadStores(block, lva.GetLiveOut(block), lva.CapturedVariables, node, declaration, context);
            }
        }

        private static void CheckCfgBlockForDeadStores(Block block, IEnumerable<ISymbol> blockOutState, IEnumerable<ISymbol> excludedLocals, CSharpSyntaxNode node,
            ISymbol declaration, SyntaxNodeAnalysisContext context)
        {
            var lva = new InBlockLivenessAnalysis(block, blockOutState, excludedLocals, node, declaration, context);
            lva.Analyze();
        }

        private class InBlockLivenessAnalysis
        {
            private readonly Block block;
            private readonly IEnumerable<ISymbol> blockOutState;
            private readonly SyntaxNodeAnalysisContext context;
            private readonly ISymbol declaration;
            private readonly IEnumerable<ISymbol> excludedLocals;

            private readonly CSharpSyntaxNode node;

            public InBlockLivenessAnalysis(Block block, IEnumerable<ISymbol> blockOutState, IEnumerable<ISymbol> excludedLocals, CSharpSyntaxNode node, ISymbol declaration,
                SyntaxNodeAnalysisContext context)
            {
                this.block = block;
                this.blockOutState = blockOutState;
                this.node = node;
                this.declaration = declaration;
                this.context = context;
                this.excludedLocals = excludedLocals;
            }

            public void Analyze()
            {
                var assignmentLhs = new HashSet<SyntaxNode>();
                var liveOut = new HashSet<ISymbol>(blockOutState);

                foreach (var instruction in block.Instructions.Reverse())
                {
                    switch (instruction.Kind())
                    {
                        case SyntaxKind.IdentifierName:
                            ProcessIdentifier(instruction, assignmentLhs, liveOut);
                            break;

                        case SyntaxKind.AddAssignmentExpression:
                        case SyntaxKind.SubtractAssignmentExpression:
                        case SyntaxKind.MultiplyAssignmentExpression:
                        case SyntaxKind.DivideAssignmentExpression:
                        case SyntaxKind.ModuloAssignmentExpression:
                        case SyntaxKind.AndAssignmentExpression:
                        case SyntaxKind.ExclusiveOrAssignmentExpression:
                        case SyntaxKind.OrAssignmentExpression:
                        case SyntaxKind.LeftShiftAssignmentExpression:
                        case SyntaxKind.RightShiftAssignmentExpression:
                            ProcessOpAssignment(instruction, assignmentLhs, liveOut);
                            break;

                        case SyntaxKind.SimpleAssignmentExpression:
                            ProcessSimpleAssignment(instruction, assignmentLhs, liveOut);
                            break;

                        case SyntaxKind.VariableDeclarator:
                            ProcessVariableDeclarator(instruction, liveOut);
                            break;

                        case SyntaxKind.PreIncrementExpression:
                        case SyntaxKind.PreDecrementExpression:
                            ProcessPrefixExpression(instruction, liveOut);
                            break;

                        case SyntaxKind.PostIncrementExpression:
                        case SyntaxKind.PostDecrementExpression:
                            ProcessPostfixExpression(instruction, liveOut);
                            break;

                        default:
                            break;
                    }
                }
            }

            private void ProcessIdentifier(SyntaxNode instruction, HashSet<SyntaxNode> assignmentLhs, HashSet<ISymbol> liveOut)
            {
                var identifier = (IdentifierNameSyntax)instruction;
                var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
                if (!IsSymbolRelevant(symbol))
                {
                    return;
                }

                if (!identifier.GetSelfOrTopParenthesizedExpression().IsInNameofCall(context.SemanticModel) &&
                    LiveVariableAnalysis.IsLocalScoped(symbol, declaration))
                {
                    if (LiveVariableAnalysis.IsOutArgument(identifier))
                    {
                        liveOut.Remove(symbol);
                    }
                    else
                    {
                        if (!assignmentLhs.Contains(identifier))
                        {
                            liveOut.Add(symbol);
                        }
                    }
                }
            }

            private void ProcessOpAssignment(SyntaxNode instruction, HashSet<SyntaxNode> assignmentLhs, HashSet<ISymbol> liveOut)
            {
                var assignment = (AssignmentExpressionSyntax)instruction;
                var left = assignment.Left.RemoveParentheses();
                if (left.IsKind(SyntaxKind.IdentifierName))
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(left).Symbol;
                    if (!IsSymbolRelevant(symbol))
                    {
                        return;
                    }

                    ReportOnAssignment(assignment, left, symbol, declaration, assignmentLhs, liveOut, context);
                }
            }

            private void ProcessSimpleAssignment(SyntaxNode instruction, HashSet<SyntaxNode> assignmentLhs, HashSet<ISymbol> liveOut)
            {
                var assignment = (AssignmentExpressionSyntax)instruction;
                var left = assignment.Left.RemoveParentheses();
                if (left.IsKind(SyntaxKind.IdentifierName))
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(left).Symbol;
                    if (!IsSymbolRelevant(symbol))
                    {
                        return;
                    }

                    ReportOnAssignment(assignment, left, symbol, declaration, assignmentLhs, liveOut, context);
                    liveOut.Remove(symbol);
                }
            }

            private void ProcessVariableDeclarator(SyntaxNode instruction, HashSet<ISymbol> liveOut)
            {
                var declarator = (VariableDeclaratorSyntax)instruction;
                var symbol = context.SemanticModel.GetDeclaredSymbol(declarator);
                if (!IsSymbolRelevant(symbol))
                {
                    return;
                }

                if (declarator.Initializer != null &&
                    !liveOut.Contains(symbol) &&
                    !IsUnusedLocal(symbol))
                {
                    var location = GetFirstLineLocationFromToken(declarator.Initializer.EqualsToken, declarator.Initializer);
                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, symbol.Name));
                }
                liveOut.Remove(symbol);
            }

            private bool IsUnusedLocal(ISymbol declaredSymbol)
            {
                return node.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .SelectMany(identifier => VariableUnused.GetUsedSymbols(identifier, context.SemanticModel))
                    .All(s => !s.Equals(declaredSymbol));
            }

            private void ProcessPrefixExpression(SyntaxNode instruction, HashSet<ISymbol> liveOut)
            {
                var prefixExpression = (PrefixUnaryExpressionSyntax)instruction;
                var parent = prefixExpression.GetSelfOrTopParenthesizedExpression();
                var operand = prefixExpression.Operand.RemoveParentheses();
                if (parent.Parent is ExpressionStatementSyntax &&
                    operand.IsKind(SyntaxKind.IdentifierName))
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(operand).Symbol;
                    if (!IsSymbolRelevant(symbol))
                    {
                        return;
                    }

                    if (LiveVariableAnalysis.IsLocalScoped(symbol, declaration) &&
                        !liveOut.Contains(symbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rule, prefixExpression.GetLocation(), symbol.Name));
                    }
                }
            }

            private void ProcessPostfixExpression(SyntaxNode instruction, HashSet<ISymbol> liveOut)
            {
                var postfixExpression = (PostfixUnaryExpressionSyntax)instruction;
                var operand = postfixExpression.Operand.RemoveParentheses();
                if (operand.IsKind(SyntaxKind.IdentifierName))
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(operand).Symbol;
                    if (!IsSymbolRelevant(symbol))
                    {
                        return;
                    }

                    if (LiveVariableAnalysis.IsLocalScoped(symbol, declaration) &&
                        !liveOut.Contains(symbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rule, postfixExpression.GetLocation(), symbol.Name));
                    }
                }
            }

            private static void ReportOnAssignment(AssignmentExpressionSyntax assignment, ExpressionSyntax left, ISymbol symbol,
                ISymbol declaration, HashSet<SyntaxNode> assignmentLhs, HashSet<ISymbol> outState, SyntaxNodeAnalysisContext context)
            {
                if (LiveVariableAnalysis.IsLocalScoped(symbol, declaration) &&
                    !outState.Contains(symbol))
                {
                    var location = GetFirstLineLocationFromToken(assignment.OperatorToken, assignment.Right);
                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, symbol.Name));
                }

                assignmentLhs.Add(left);
            }

            private static Location GetFirstLineLocationFromToken(SyntaxToken issueStartToken, SyntaxNode wholeIssue)
            {
                var line = GetLineOfToken(issueStartToken, wholeIssue.SyntaxTree);
                var rightSingleLine = line.Span.Intersection(
                    TextSpan.FromBounds(issueStartToken.SpanStart, wholeIssue.Span.End));

                return Location.Create(wholeIssue.SyntaxTree,
                    TextSpan.FromBounds(
                        issueStartToken.SpanStart,
                        rightSingleLine.HasValue ? rightSingleLine.Value.End : issueStartToken.Span.End));
            }

            private bool IsSymbolRelevant(ISymbol symbol)
            {
                return symbol != null &&
                    !excludedLocals.Contains(symbol);
            }

            private static TextLine GetLineOfToken(SyntaxToken token, SyntaxTree tree)
            {
                return tree.GetText().Lines[token.GetLocation().GetLineSpan().StartLinePosition.Line];
            }
        }
    }
}
