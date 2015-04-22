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
using System;
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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("misra", "pitfall")]
    public class ForLoopCounterChanged : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S127";
        internal const string Description = "\"for\" loop stop conditions should be invariant";
        internal const string MessageFormat = "Do not update the loop counter \"{0}\" within the loop body.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major; 
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS127");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private sealed class SideEffectExpression
        {
            public IImmutableList<SyntaxKind> Kinds;
            public Func<SyntaxNode, SyntaxNode> AffectedExpression;
        }

        private static readonly IImmutableList<SideEffectExpression> SideEffectExpressions = ImmutableArray.Create(
            new SideEffectExpression
            {
                Kinds = ImmutableArray.Create(SyntaxKind.PreIncrementExpression, SyntaxKind.PreDecrementExpression),
                AffectedExpression = node => ((PrefixUnaryExpressionSyntax)node).Operand
            },
            new SideEffectExpression
            {
                Kinds = ImmutableArray.Create(SyntaxKind.PostIncrementExpression, SyntaxKind.PostDecrementExpression),
                AffectedExpression = node => ((PostfixUnaryExpressionSyntax)node).Operand
            },
            new SideEffectExpression
            {
                Kinds = ImmutableArray.Create(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxKind.AddAssignmentExpression,
                    SyntaxKind.SubtractAssignmentExpression,
                    SyntaxKind.MultiplyAssignmentExpression,
                    SyntaxKind.DivideAssignmentExpression,
                    SyntaxKind.ModuloAssignmentExpression,
                    SyntaxKind.AndAssignmentExpression,
                    SyntaxKind.ExclusiveOrAssignmentExpression,
                    SyntaxKind.OrAssignmentExpression,
                    SyntaxKind.LeftShiftAssignmentExpression,
                    SyntaxKind.RightShiftAssignmentExpression),
                AffectedExpression = node => ((AssignmentExpressionSyntax)node).Left
            });

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var forNode = (ForStatementSyntax)c.Node;
                    var loopCounters = LoopCounters(forNode, c.SemanticModel).ToList();

                    foreach (var affectedExpression in AffectedExpressions(forNode.Statement))
                    {
                        var symbol = c.SemanticModel.GetSymbolInfo(affectedExpression).Symbol;
                        if (symbol != null && loopCounters.Contains(symbol))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, affectedExpression.GetLocation(), affectedExpression.ToString()/*symbol.OriginalDefinition.Name*/));
                        }
                    }
                },
                SyntaxKind.ForStatement);
        }

        private static IEnumerable<ISymbol> LoopCounters(ForStatementSyntax node, SemanticModel semanticModel)
        {
            var declaredVariables = node.Declaration == null
                ? Enumerable.Empty<ISymbol>()
                : node.Declaration.Variables
                    .Select(v => semanticModel.GetDeclaredSymbol(v));

            var initializedVariables = node.Initializers
                .Where(i => i.IsKind(SyntaxKind.SimpleAssignmentExpression))
                .Select(i => semanticModel.GetSymbolInfo(((AssignmentExpressionSyntax)i).Left).Symbol);

            return declaredVariables.Union(initializedVariables);
        }

        private static IEnumerable<SyntaxNode> AffectedExpressions(SyntaxNode node)
        {
            return node
                .DescendantNodesAndSelf()
                .Where(n => SideEffectExpressions.Any(s => s.Kinds.Any(n.IsKind)))
                .Select(n => SideEffectExpressions.Single(s => s.Kinds.Any(n.IsKind)).AffectedExpression(n));
        }
    }
}
