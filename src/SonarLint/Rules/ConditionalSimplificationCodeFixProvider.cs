/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class ConditionalSimplificationCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Simplify condition";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(ConditionalSimplification.DiagnosticId);
            }
        }
        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var syntax = root.FindNode(diagnosticSpan);

            var conditional = syntax as ConditionalExpressionSyntax;
            if (conditional != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        c =>
                        {
                            var condition = TernaryOperatorPointless.RemoveParentheses(conditional.Condition);
                            var whenTrue = TernaryOperatorPointless.RemoveParentheses(conditional.WhenTrue);
                            var whenFalse = TernaryOperatorPointless.RemoveParentheses(conditional.WhenFalse);

                            ExpressionSyntax compared;
                            bool comparedIsNullInTrue;
                            ConditionalSimplification.TryGetComparedVariable(condition, out compared, out comparedIsNullInTrue);

                            var newRoot = root.ReplaceNode(conditional, GetNullCoalescing(whenTrue, whenFalse, compared)
                                    .WithTriviaFrom(conditional))
                                .WithAdditionalAnnotations(Formatter.Annotation);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        }),
                    context.Diagnostics);
            }

            var ifStatement = syntax as IfStatementSyntax;
            if (ifStatement != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        c =>
                        {
                            var whenTrue = ConditionalSimplification.ExtractSingleStatement(ifStatement.Statement);
                            var whenFalse = ConditionalSimplification.ExtractSingleStatement(ifStatement.Else.Statement);

                            ExpressionSyntax compared;
                            bool comparedIsNullInTrue;
                            ConditionalSimplification.TryGetComparedVariable(ifStatement.Condition, out compared, out comparedIsNullInTrue);

                            var isNullCoalescing = bool.Parse(diagnostic.Properties[ConditionalSimplification.IsNullCoalescingKey]);

                            var newRoot = root.ReplaceNode(ifStatement,
                                GetSimplified(whenTrue, whenFalse, ifStatement.Condition, compared, isNullCoalescing)
                                    .WithTriviaFrom(ifStatement))
                                .WithAdditionalAnnotations(Formatter.Annotation);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        }),
                    context.Diagnostics);
            }
        }

        private static StatementSyntax GetSimplified(StatementSyntax statement1, StatementSyntax statement2,
            ExpressionSyntax condition, ExpressionSyntax compared, bool isNullCoalescing)
        {
            var return1 = statement1 as ReturnStatementSyntax;
            var return2 = statement2 as ReturnStatementSyntax;

            if (return1 != null && return2 != null)
            {
                var retExpr1 = TernaryOperatorPointless.RemoveParentheses(return1.Expression);
                var retExpr2 = TernaryOperatorPointless.RemoveParentheses(return2.Expression);

                if (isNullCoalescing)
                {
                    var nullCoalescing = GetNullCoalescing(retExpr1, retExpr2, compared);
                    return SyntaxFactory.ReturnStatement(nullCoalescing);
                }

                return
                    SyntaxFactory.ReturnStatement(
                        SyntaxFactory.ConditionalExpression(
                            condition,
                            return1.Expression,
                            return2.Expression));
            }

            var expressionStatement1 = statement1 as ExpressionStatementSyntax;
            var expressionStatement2 = statement2 as ExpressionStatementSyntax;

            var expression1 = TernaryOperatorPointless.RemoveParentheses(expressionStatement1.Expression);
            var expression2 = TernaryOperatorPointless.RemoveParentheses(expressionStatement2.Expression);

            var assignment = GetSimplifiedAssignment(expression1, expression2, condition, compared, isNullCoalescing);
            if (assignment != null)
            {
                return SyntaxFactory.ExpressionStatement(assignment);
            }

            return SyntaxFactory.ExpressionStatement(
                GetSimplificationFromInvocations(expression1, expression2, condition, compared, isNullCoalescing));
        }

        private static ExpressionSyntax GetSimplifiedAssignment(ExpressionSyntax expression1, ExpressionSyntax expression2,
            ExpressionSyntax condition, ExpressionSyntax compared, bool isNullCoalescing)
        {
            var assignment1 = expression1 as AssignmentExpressionSyntax;
            var assignment2 = expression2 as AssignmentExpressionSyntax;
            var canBeSimplified =
                assignment1 != null &&
                assignment2 != null &&
                EquivalenceChecker.AreEquivalent(assignment1.Left, assignment2.Left) &&
                assignment1.Kind() == assignment2.Kind();

            if (!canBeSimplified)
            {
                return null;
            }

            var expression = isNullCoalescing
                ? GetNullCoalescing(assignment1.Right, assignment2.Right, compared)
                : SyntaxFactory.ConditionalExpression(
                    condition,
                    assignment1.Right,
                    assignment2.Right);

            return SyntaxFactory.AssignmentExpression(
                assignment1.Kind(),
                assignment1.Left,
                expression);
        }

        private static ExpressionSyntax GetNullCoalescing(ExpressionSyntax whenTrue, ExpressionSyntax whenFalse,
            ExpressionSyntax compared)
        {
            if (EquivalenceChecker.AreEquivalent(whenTrue, compared))
            {
                return SyntaxFactory.BinaryExpression(
                    SyntaxKind.CoalesceExpression,
                    compared,
                    whenFalse);
            }

            if (EquivalenceChecker.AreEquivalent(whenFalse, compared))
            {
                return SyntaxFactory.BinaryExpression(
                    SyntaxKind.CoalesceExpression,
                    compared,
                    whenTrue);
            }

            return GetSimplificationFromInvocations(whenTrue, whenFalse, null, compared, true);
        }

        private static ExpressionSyntax GetSimplificationFromInvocations(ExpressionSyntax expression1, ExpressionSyntax expression2,
            ExpressionSyntax condition, ExpressionSyntax compared, bool isNullCoalescing)
        {
            var methodCall1 = expression1 as InvocationExpressionSyntax;
            var methodCall2 = expression2 as InvocationExpressionSyntax;

            var newArgumentList = SyntaxFactory.ArgumentList();

            for (int i = 0; i < methodCall1.ArgumentList.Arguments.Count; i++)
            {
                var arg1 = methodCall1.ArgumentList.Arguments[i];
                var arg2 = methodCall2.ArgumentList.Arguments[i];

                if (!EquivalenceChecker.AreEquivalent(arg1.Expression, arg2.Expression))
                {
                    if (isNullCoalescing)
                    {
                        var arg1IsCompared = EquivalenceChecker.AreEquivalent(arg1.Expression, compared);
                        var expression = arg1IsCompared ? arg2.Expression : arg1.Expression;

                        newArgumentList = newArgumentList.AddArguments(
                            SyntaxFactory.Argument(
                                arg1.NameColon,
                                arg1.RefOrOutKeyword,
                                SyntaxFactory.BinaryExpression(
                                    SyntaxKind.CoalesceExpression,
                                    compared,
                                    expression)));
                    }
                    else
                    {
                        newArgumentList = newArgumentList.AddArguments(
                            SyntaxFactory.Argument(
                                arg1.NameColon,
                                arg1.RefOrOutKeyword,
                                SyntaxFactory.ConditionalExpression(
                                    condition,
                                    arg1.Expression,
                                    arg2.Expression)));
                    }
                }
                else
                {
                    newArgumentList = newArgumentList.AddArguments(arg1);
                }
            }

            return methodCall1.WithArgumentList(newArgumentList);
        }
    }
}

