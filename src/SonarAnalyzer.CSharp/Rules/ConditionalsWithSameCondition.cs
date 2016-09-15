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

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Helpers.CSharp;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy, Tag.Suspicious)]
    public class ConditionalsWithSameCondition : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2760";
        internal const string Title = "Sequential tests should not check the same condition";
        internal const string Description =
            "When the same condition is checked twice in a row, it is either confusing - why have separate checks? - or an error - some other " +
            "condition should have been checked in the second test.";
        internal const string MessageFormat = "This condition was just checked on line {0}.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

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
                    CheckMatchingExpressionsInSucceedingStatements((IfStatementSyntax)c.Node, syntax => syntax.Condition, c);
                },
                SyntaxKind.IfStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    CheckMatchingExpressionsInSucceedingStatements((SwitchStatementSyntax)c.Node, syntax => syntax.Expression, c);
                },
                SyntaxKind.SwitchStatement);
        }

        private static void CheckMatchingExpressionsInSucceedingStatements<T>(T statement,
            Func<T, ExpressionSyntax> expressionSelector, SyntaxNodeAnalysisContext context) where T : StatementSyntax
        {
            var previousStatement = statement.GetPrecedingStatement() as T;
            if (previousStatement == null)
            {
                return;
            }

            var currentExpression = expressionSelector(statement);
            var previousExpression = expressionSelector(previousStatement);

            if (EquivalenceChecker.AreEquivalent(currentExpression, previousExpression) &&
                !ContainsPossibleUpdate(previousStatement, currentExpression, context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, currentExpression.GetLocation(),
                    previousExpression.GetLineNumberToReport()));
            }
        }

        private static bool ContainsPossibleUpdate(StatementSyntax statement, ExpressionSyntax expression,
            SemanticModel semanticModel)
        {
            var checkedSymbols = expression.DescendantNodesAndSelf()
                .Select(node => semanticModel.GetSymbolInfo(node).Symbol)
                .Where(symbol => symbol != null)
                .ToImmutableHashSet();

            var statementDescendents = statement.DescendantNodesAndSelf().ToList();
            var isAnyCheckedSymbolUpdated = statementDescendents
                .OfType<AssignmentExpressionSyntax>()
                .Any(assignment => IsCheckedSymbolUpdated(assignment.Left, checkedSymbols, semanticModel));

            if (isAnyCheckedSymbolUpdated)
            {
                return true;
            }

            var postfixUnaryExpression = statementDescendents
                .OfType<PostfixUnaryExpressionSyntax>()
                .Any(expressionSyntax =>
                {
                    var symbol = semanticModel.GetSymbolInfo(expressionSyntax.Operand).Symbol;
                    return symbol != null && checkedSymbols.Contains(symbol);
                });

            if (postfixUnaryExpression)
            {
                return true;
            }

            var prefixUnaryExpression = statementDescendents
                .OfType<PrefixUnaryExpressionSyntax>()
                .Any(expressionSyntax =>
                {
                    var symbol = semanticModel.GetSymbolInfo(expressionSyntax.Operand).Symbol;
                    return symbol != null && checkedSymbols.Contains(symbol);
                });

            return prefixUnaryExpression;
        }

        private static bool IsCheckedSymbolUpdated(ExpressionSyntax expression, ImmutableHashSet<ISymbol> checkedSymbols, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
            return symbol != null && checkedSymbols.Contains(symbol);
        }
    }
}
