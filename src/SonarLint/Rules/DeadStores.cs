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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious, Tag.Cert, Tag.Cwe, Tag.Unused)]
    public class DeadStores : DiagnosticAnalyzer
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
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        private static readonly SyntaxKind[] ReadWriteAssignment =
        {
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.RightShiftAssignmentExpression
        };

        private static readonly SyntaxKind[] LoopKinds =
        {
            SyntaxKind.ForEachStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement
        };

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var variableDeclaration = (VariableDeclarationSyntax)c.Node;
                    var localDeclarationStatementSyntax = variableDeclaration.Parent;

                    var declaringBlock = localDeclarationStatementSyntax.Parent as BlockSyntax;
                    if (declaringBlock == null)
                    {
                        return;
                    }

                    foreach (var variableDeclarator in variableDeclaration.Variables)
                    {
                        var variableSymbol = c.SemanticModel.GetDeclaredSymbol(variableDeclarator);
                        if (variableSymbol == null)
                        {
                            continue;
                        }

                        var references = declaringBlock.DescendantNodes()
                            .OfType<IdentifierNameSyntax>()
                            .Where(syntax => syntax.SpanStart > variableDeclarator.FullSpan.End)
                            .Where(syntax =>
                                variableSymbol.Equals(c.SemanticModel.GetSymbolInfo(syntax).Symbol))
                            .ToList();

                        if (references.Count == 0)
                        {
                            if (variableDeclarator.Initializer != null)
                            {
                                c.ReportDiagnostic(Diagnostic.Create(Rule, variableDeclarator.Initializer.EqualsToken.GetLocation(),
                                    variableDeclarator.Identifier.Text));
                            }
                            continue;
                        }

                        var assignments = declaringBlock.DescendantNodes()
                            .OfType<AssignmentExpressionSyntax>()
                            .Where(assignment =>
                                variableSymbol.Equals(c.SemanticModel.GetSymbolInfo(assignment.Left).Symbol))
                            .ToList();

                        if (assignments.Count == 0)
                        {
                            continue;
                        }

                        var firstAssignment = assignments.First();
                        if (variableDeclarator.Initializer != null &&
                            InAnonymous(firstAssignment, declaringBlock))
                        {
                            return;
                        }

                        if (variableDeclarator.Initializer != null &&
                            !MightHaveReferenceBetween(variableDeclarator.Initializer, firstAssignment, references) &&
                            !InConditional(firstAssignment, declaringBlock))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, variableDeclarator.Initializer.EqualsToken.GetLocation(),
                                variableDeclarator.Identifier.Text));
                        }

                        for (var i = 1; i < assignments.Count; i++)
                        {
                            var first = assignments[i - 1];
                            var second = assignments[i];

                            if (InConditional(second, declaringBlock))
                            {
                                continue;
                            }

                            if (InAnonymous(second, declaringBlock))
                            {
                                return;
                            }

                            if (MightHaveReferenceBetween(first, second, references))
                            {
                                continue;
                            }

                            c.ReportDiagnostic(Diagnostic.Create(Rule, first.OperatorToken.GetLocation(),
                                variableDeclarator.Identifier.Text));
                        }

                        var lastAssignment = assignments.Last();

                        if (InAnonymous(lastAssignment, declaringBlock))
                        {
                            return;
                        }

                        if (!references.Any(reference => reference.SpanStart > lastAssignment.Span.End) &&
                            !InLoop(lastAssignment, declaringBlock))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, lastAssignment.OperatorToken.GetLocation(),
                                variableDeclarator.Identifier.Text));
                        }
                    }
                },
                SyntaxKind.VariableDeclaration);
        }

        private static bool MightHaveReferenceBetween(SyntaxNode first, AssignmentExpressionSyntax second, List<IdentifierNameSyntax> references)
        {
            return !DifferentStatementsWithSameParent(first, second) ||
                   references.Any(reference =>
                       reference.SpanStart < second.SpanStart && reference.SpanStart >= first.Span.End) ||
                   ReferenceInAssignmentRight(references, second) ||
                   ReadWriteAssignment.Contains(second.Kind());
        }

        private static bool InAnonymous(AssignmentExpressionSyntax currentAssignment, BlockSyntax declaringBlock)
        {
            return currentAssignment.Ancestors()
                .TakeWhile(ancestor => ancestor != declaringBlock)
                .Any(ancestor => ancestor is AnonymousFunctionExpressionSyntax);
        }

        private static bool InConditional(AssignmentExpressionSyntax currentAssignment, BlockSyntax declaringBlock)
        {
            return currentAssignment.Ancestors()
                .TakeWhile(ancestor => ancestor != declaringBlock)
                .OfType<BinaryExpressionSyntax>()
                .Any(binary => binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                                 binary.IsKind(SyntaxKind.LogicalOrExpression));
        }

        private static bool InLoop(AssignmentExpressionSyntax currentAssignment, BlockSyntax declaringBlock)
        {
            return currentAssignment.Ancestors()
                .TakeWhile(ancestor => ancestor != declaringBlock)
                .Any(ancestor => LoopKinds.Contains(ancestor.Kind()));
        }

        private static bool DifferentStatementsWithSameParent(SyntaxNode first, SyntaxNode second)
        {
            var firstStatement = first.FirstAncestorOrSelf<StatementSyntax>();
            var secondStatement = second.FirstAncestorOrSelf<StatementSyntax>();

            return firstStatement != secondStatement && firstStatement.Parent == secondStatement.Parent;
        }

        private static bool ReferenceInAssignmentRight(List<IdentifierNameSyntax> references, AssignmentExpressionSyntax currentAssignment)
        {
            return references.Any(reference =>
                reference.SpanStart >= currentAssignment.Right.SpanStart &&
                reference.SpanStart < currentAssignment.Right.Span.End);
        }
    }
}
