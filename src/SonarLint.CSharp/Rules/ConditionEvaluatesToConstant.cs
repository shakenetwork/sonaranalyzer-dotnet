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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Linq;
using System;
using SonarLint.Helpers.FlowAnalysis.Common;
using SonarLint.Helpers.FlowAnalysis.CSharp;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    using LiveVariableAnalysis = Helpers.FlowAnalysis.CSharp.LiveVariableAnalysis;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Cert, Tag.Cwe, Tag.Misra)]
    public class ConditionEvaluatesToConstant : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2583";
        internal const string Title = "Conditions should not unconditionally evaluate to \"true\" or to \"false\"";
        internal const string Description =
            "Conditional statements using a condition which cannot be anything but \"false\" have the effect of making blocks of code " +
            "non-functional. If the condition cannot evaluate to anything but \"true\", the conditional statement is completely " +
            "redundant, and makes the code less readable. It is quite likely that the code does not match the programmer's intent. " +
            "Either the condition should be removed or it should be updated so that it does not always evaluate to \"true\" or \"false\".";
        internal const string MessageFormat = "Change this condition so that it does not always evaluate to \"{0}\".";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

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

                    CheckForRedundantConditions(declaration.Body, symbol, c);
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

                    CheckForRedundantConditions(declaration.Body, symbol, c);
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

                    CheckForRedundantConditions(declaration.Body, symbol, c);
                },
                SyntaxKind.AnonymousMethodExpression,
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression);
        }

        private static void CheckForRedundantConditions(CSharpSyntaxNode body, ISymbol declaration, SyntaxNodeAnalysisContext context)
        {
            IControlFlowGraph cfg;
            if (!ControlFlowGraph.TryGet(body, context.SemanticModel, out cfg))
            {
                return;
            }

            var lva = LiveVariableAnalysis.Analyze(cfg, declaration, context.SemanticModel);

            var explodedGraph = new ExplodedGraph(cfg, declaration, context.SemanticModel, lva);
            var conditionTrue = new HashSet<SyntaxNode>();
            var conditionFalse = new HashSet<SyntaxNode>();

            EventHandler<ConditionEvaluatedEventArgs> collectConditions =
                (sender, args) => CollectConditions(args, conditionTrue, conditionFalse);

            EventHandler explorationEnded =
                (sender, args) => ProcessVisitedBlocks(conditionTrue, conditionFalse, context);

            explodedGraph.ExplorationEnded += explorationEnded;
            explodedGraph.ConditionEvaluated += collectConditions;

            try
            {
                explodedGraph.Walk();
            }
            finally
            {
                explodedGraph.ExplorationEnded -= explorationEnded;
                explodedGraph.ConditionEvaluated -= collectConditions;
            }
        }

        private static void ProcessVisitedBlocks(HashSet<SyntaxNode> conditionTrue, HashSet<SyntaxNode> conditionFalse, SyntaxNodeAnalysisContext context)
        {
            foreach (var alwaysTrue in conditionTrue.Except(conditionFalse))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, alwaysTrue.GetLocation(), "true"));
            }

            foreach (var alwaysFalse in conditionFalse.Except(conditionTrue))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, alwaysFalse.GetLocation(), "false"));
            }
        }

        private static void CollectConditions(ConditionEvaluatedEventArgs args, HashSet<SyntaxNode> conditionTrue, HashSet<SyntaxNode> conditionFalse)
        {
            if (args.Condition == null ||
                args.Condition.IsKind(SyntaxKind.TrueLiteralExpression) ||
                args.Condition.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                return;
            }

            if (args.EvaluationValue)
            {
                conditionTrue.Add(args.Condition);
            }
            else
            {
                conditionFalse.Add(args.Condition);
            }
        }
    }
}
