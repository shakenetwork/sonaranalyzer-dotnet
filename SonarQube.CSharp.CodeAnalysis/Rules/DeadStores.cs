/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
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
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug", "cert", "cwe", "unused")]
    public class DeadStores : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1854";
        internal const string Description = "Dead stores should be removed";
        internal const string MessageFormat = "Remove this useless assignment to local variable \"{0}\".";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS1854");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        public class AssignmentWithParentStatement
        {
            public AssignmentExpressionSyntax Assignment { get; set; }
            public StatementSyntax ParentStatement { get; set; }
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
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var variableDeclarator = (VariableDeclaratorSyntax)c.Node;
                    var variableDeclaration = variableDeclarator.Parent;
                    var localDeclarationStatementSyntax = variableDeclaration.Parent;

                    var declaringBlock = localDeclarationStatementSyntax.Parent as BlockSyntax;
                    if (declaringBlock == null)
                    {
                        return;
                    }

                    var variableSymbol = c.SemanticModel.GetDeclaredSymbol(variableDeclarator);

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
                            c.ReportDiagnostic(Diagnostic.Create(Rule, variableDeclarator.Initializer.GetLocation(),
                                variableDeclarator.Identifier.Text));
                        }

                        return;
                    }

                    var assignments =
                        declaringBlock.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                            .Where(assignment =>
                                c.SemanticModel.GetSymbolInfo(assignment.Left).Symbol.Equals(variableSymbol))
                            .ToList();

                    if (assignments.Count == 0)
                    {
                        return;
                    }

                    {
                        var first = variableDeclarator.Initializer;
                        var second = assignments.First();
                        if (first != null &&
                            !MightHaveReferenceBetween(first, second, references))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, first.GetLocation(),
                                variableDeclarator.Identifier.Text));
                        }
                    }

                    for (var i = 1; i < assignments.Count; i++)
                    {
                        var first = assignments[i - 1];
                        var second = assignments[i];
                        

                        if (MightHaveReferenceBetween(first, second, references))
                        {
                            continue;
                        }
                        
                        c.ReportDiagnostic(Diagnostic.Create(Rule, first.GetLocation(),
                                variableDeclarator.Identifier.Text));
                    }

                    {
                        var currentAssignment = assignments.Last();
                        if (!references.Any(reference => reference.SpanStart > currentAssignment.Span.End) &&
                            !InLoop(currentAssignment, declaringBlock))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, currentAssignment.GetLocation(),
                                variableDeclarator.Identifier.Text));
                        }
                    }
                },
                SyntaxKind.VariableDeclarator);
        }

        private static bool MightHaveReferenceBetween(SyntaxNode first, AssignmentExpressionSyntax second, List<IdentifierNameSyntax> references)
        {
            return !DifferentStatementsWithSameParent(first, second) ||
                   references.Any(reference =>
                       reference.SpanStart < second.SpanStart && reference.SpanStart >= first.Span.End) ||
                   ReferenceInAssignmentRight(references, second) ||
                   ReadWriteAssignment.Contains(second.Kind());
        }

        private bool InLoop(AssignmentExpressionSyntax currentAssignment, BlockSyntax declaringBlock)
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
