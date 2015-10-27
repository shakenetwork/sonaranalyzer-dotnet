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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace SonarLint.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class BooleanLiteralUnnecessaryCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Remove the unnecessary Boolean literal(s)";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(BooleanLiteralUnnecessary.DiagnosticId);
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
            var syntaxNode = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true);

            var binary = syntaxNode as BinaryExpressionSyntax;
            if (binary != null)
            {
                RegisterBinaryExpressionReplacement(context, root, syntaxNode, binary);
                return;
            }

            var conditional = syntaxNode as ConditionalExpressionSyntax;
            if (conditional != null)
            {
                RegisterConditionalExpressionRemoval(context, root, conditional);
                return;
            }

            var literal = syntaxNode as LiteralExpressionSyntax;
            if (literal == null)
            {
                return;
            }

            if (literal.Parent is PrefixUnaryExpressionSyntax)
            {
                RegisterBooleanInversion(context, root, literal);
                return;
            }

            var conditionalParent = literal.Parent as ConditionalExpressionSyntax;
            if (conditionalParent != null)
            {
                RegisterConditionalExpressionRewrite(context, root, literal, conditionalParent);
                return;
            }

            var binaryParent = literal.Parent as BinaryExpressionSyntax;
            if (binaryParent != null)
            {
                RegisterBinaryExpressionRemoval(context, root, literal, binaryParent);
                return;
            }
        }

        private static void RegisterBinaryExpressionRemoval(CodeFixContext context, SyntaxNode root, LiteralExpressionSyntax literal, BinaryExpressionSyntax binaryParent)
        {
            var otherNode = binaryParent.Left.Equals(literal)
                ? binaryParent.Right
                : binaryParent.Left;

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var newExpression = GetNegatedExpression(otherNode);
                        var newRoot = root.ReplaceNode(binaryParent, newExpression
                            .WithAdditionalAnnotations(Formatter.Annotation));

                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }

        private static void RegisterConditionalExpressionRewrite(CodeFixContext context, SyntaxNode root, LiteralExpressionSyntax literal, ConditionalExpressionSyntax conditionalParent)
        {
            context.RegisterCodeFix(
                                CodeAction.Create(
                                    Title,
                                    c => RewriteConditional(context.Document, root, literal, conditionalParent)),
                                context.Diagnostics);
        }

        private static void RegisterBooleanInversion(CodeFixContext context, SyntaxNode root, LiteralExpressionSyntax literal)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c => RemovePrefixUnary(context.Document, root, literal)),
                context.Diagnostics);
        }

        private static void RegisterConditionalExpressionRemoval(CodeFixContext context, SyntaxNode root, ConditionalExpressionSyntax conditional)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c => RemoveConditional(context.Document, root, conditional)),
                context.Diagnostics);
        }

        private static void RegisterBinaryExpressionReplacement(CodeFixContext context, SyntaxNode root, SyntaxNode syntaxNode, BinaryExpressionSyntax binary)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var keepThisNode = FindNodeToKeep(binary);
                        var newRoot = root.ReplaceNode(syntaxNode, keepThisNode
                            .WithAdditionalAnnotations(Formatter.Annotation));
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }

        private static SyntaxNode FindNodeToKeep(BinaryExpressionSyntax binary)
        {
            #region logical and false, logical or true

            if (binary.IsKind(SyntaxKind.LogicalAndExpression) &&
                (EquivalenceChecker.AreEquivalent(binary.Left, BooleanLiteralUnnecessary.FalseExpression) ||
                 EquivalenceChecker.AreEquivalent(binary.Right, BooleanLiteralUnnecessary.FalseExpression)))
            {
                return BooleanLiteralUnnecessary.FalseExpression;
            }
            if (binary.IsKind(SyntaxKind.LogicalOrExpression) &&
                (EquivalenceChecker.AreEquivalent(binary.Left, BooleanLiteralUnnecessary.TrueExpression) ||
                 EquivalenceChecker.AreEquivalent(binary.Right, BooleanLiteralUnnecessary.TrueExpression)))
            {
                return BooleanLiteralUnnecessary.TrueExpression;
            }

            #endregion

            #region ==/!= both sides booleans

            if (binary.IsKind(SyntaxKind.EqualsExpression) &&
                TwoSidesAreDifferentBooleans(binary))
            {
                return BooleanLiteralUnnecessary.FalseExpression;
            }
            if (binary.IsKind(SyntaxKind.EqualsExpression) &&
                TwoSidesAreSameBooleans(binary))
            {
                return BooleanLiteralUnnecessary.TrueExpression;
            }
            if (binary.IsKind(SyntaxKind.NotEqualsExpression) &&
                TwoSidesAreSameBooleans(binary))
            {
                return BooleanLiteralUnnecessary.FalseExpression;
            }
            if (binary.IsKind(SyntaxKind.NotEqualsExpression) &&
                TwoSidesAreDifferentBooleans(binary))
            {
                return BooleanLiteralUnnecessary.TrueExpression;
            }

            #endregion

            if (EquivalenceChecker.AreEquivalent(binary.Left, BooleanLiteralUnnecessary.TrueExpression) ||
                EquivalenceChecker.AreEquivalent(binary.Left, BooleanLiteralUnnecessary.FalseExpression))
            {
                return binary.Right;
            }
            return binary.Left;
        }

        private static bool TwoSidesAreDifferentBooleans(BinaryExpressionSyntax binary)
        {
            return (
                EquivalenceChecker.AreEquivalent(binary.Left, BooleanLiteralUnnecessary.TrueExpression) &&
                EquivalenceChecker.AreEquivalent(binary.Right, BooleanLiteralUnnecessary.FalseExpression)) ||
                (
                EquivalenceChecker.AreEquivalent(binary.Left, BooleanLiteralUnnecessary.FalseExpression) &&
                EquivalenceChecker.AreEquivalent(binary.Right, BooleanLiteralUnnecessary.TrueExpression));
        }
        private static bool TwoSidesAreSameBooleans(BinaryExpressionSyntax binary)
        {
            return (
                EquivalenceChecker.AreEquivalent(binary.Left, BooleanLiteralUnnecessary.TrueExpression) &&
                EquivalenceChecker.AreEquivalent(binary.Right, BooleanLiteralUnnecessary.TrueExpression)) ||
                (
                EquivalenceChecker.AreEquivalent(binary.Left, BooleanLiteralUnnecessary.FalseExpression) &&
                EquivalenceChecker.AreEquivalent(binary.Right, BooleanLiteralUnnecessary.FalseExpression));
        }

        private static Task<Document> RemovePrefixUnary(Document document, SyntaxNode root,
            SyntaxNode literal)
        {
            if (EquivalenceChecker.AreEquivalent(literal, BooleanLiteralUnnecessary.TrueExpression))
            {
                var newRoot = root.ReplaceNode(literal.Parent, BooleanLiteralUnnecessary.FalseExpression);
                return Task.FromResult(document.WithSyntaxRoot(newRoot));
            }
            else
            {
                var newRoot = root.ReplaceNode(literal.Parent, BooleanLiteralUnnecessary.TrueExpression);
                return Task.FromResult(document.WithSyntaxRoot(newRoot));
            }
        }

        private static Task<Document> RemoveConditional(Document document, SyntaxNode root,
            ConditionalExpressionSyntax conditional)
        {
            if (EquivalenceChecker.AreEquivalent(conditional.WhenTrue, BooleanLiteralUnnecessary.TrueExpression))
            {
                var newRoot = root.ReplaceNode(conditional,
                    conditional.Condition.WithAdditionalAnnotations(Formatter.Annotation));
                return Task.FromResult(document.WithSyntaxRoot(newRoot));
            }
            else
            {
                var newRoot = root.ReplaceNode(conditional,
                        GetNegatedExpression(conditional.Condition).WithAdditionalAnnotations(Formatter.Annotation));
                return Task.FromResult(document.WithSyntaxRoot(newRoot));
            }
        }

        private static Task<Document> RewriteConditional(Document document, SyntaxNode root, SyntaxNode syntaxNode,
            ConditionalExpressionSyntax conditional)
        {
            if (conditional.WhenTrue.Equals(syntaxNode) &&
                EquivalenceChecker.AreEquivalent(syntaxNode, BooleanLiteralUnnecessary.TrueExpression))
            {
                var newRoot = root.ReplaceNode(conditional,
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.LogicalOrExpression,
                        conditional.Condition,
                        conditional.WhenFalse)
                        .WithAdditionalAnnotations(Formatter.Annotation));

                return Task.FromResult(document.WithSyntaxRoot(newRoot));
            }

            if (conditional.WhenTrue.Equals(syntaxNode) &&
                EquivalenceChecker.AreEquivalent(syntaxNode, BooleanLiteralUnnecessary.FalseExpression))
            {
                var newRoot = root.ReplaceNode(conditional,
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.LogicalAndExpression,
                        GetNegatedExpression(conditional.Condition),
                        conditional.WhenFalse)
                    .WithAdditionalAnnotations(Formatter.Annotation));

                return Task.FromResult(document.WithSyntaxRoot(newRoot));
            }

            if (conditional.WhenFalse.Equals(syntaxNode) &&
                EquivalenceChecker.AreEquivalent(syntaxNode, BooleanLiteralUnnecessary.TrueExpression))
            {
                var newRoot = root.ReplaceNode(conditional,
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.LogicalOrExpression,
                        GetNegatedExpression(conditional.Condition),
                        conditional.WhenTrue)
                    .WithAdditionalAnnotations(Formatter.Annotation));

                return Task.FromResult(document.WithSyntaxRoot(newRoot));
            }

            if (conditional.WhenFalse.Equals(syntaxNode) &&
                EquivalenceChecker.AreEquivalent(syntaxNode, BooleanLiteralUnnecessary.FalseExpression))
            {
                var newRoot = root.ReplaceNode(conditional,
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.LogicalAndExpression,
                        conditional.Condition,
                        conditional.WhenTrue)
                    .WithAdditionalAnnotations(Formatter.Annotation));

                return Task.FromResult(document.WithSyntaxRoot(newRoot));
            }

            return Task.FromResult(document);
        }

        private static ExpressionSyntax GetNegatedExpression(ExpressionSyntax expression)
        {
            var exp = expression;
            if (expression is BinaryExpressionSyntax ||
                expression is ConditionalExpressionSyntax)
            {
                exp = SyntaxFactory.ParenthesizedExpression(expression);
            }

            return SyntaxFactory.PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                exp);
        }
    }
}
