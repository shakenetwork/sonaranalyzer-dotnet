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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Linq;
using System;
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using SonarAnalyzer.Helpers.FlowAnalysis.CSharp;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.CSharp
{
    using ExplodedGraph = Helpers.FlowAnalysis.CSharp.ExplodedGraph;

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
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
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
            context.RegisterExplodedGraphBasedAnalysis((e, c) => CheckForRedundantConditions(e, c));
        }

        private static void CheckForRedundantConditions(ExplodedGraph explodedGraph, SyntaxNodeAnalysisContext context)
        {
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
                OmittedSyntaxKinds.Contains(args.Condition.Kind()))
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

        private static readonly ISet<SyntaxKind> OmittedSyntaxKinds = ImmutableHashSet.Create(
            SyntaxKind.LogicalAndExpression,
            SyntaxKind.LogicalOrExpression,
            SyntaxKind.TrueLiteralExpression,
            SyntaxKind.FalseLiteralExpression);
    }
}
