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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Helpers.CSharp;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class ValuesUselesslyIncremented : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2123";
        internal const string Title = "Values should not be uselessly incremented";
        internal const string Description =
            "A value that is incremented or decremented and then not stored is at best wasted code and at worst a bug.";
        internal const string MessageFormat = "Remove this {0} or correct the code not to waste it.";
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
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var increment = (PostfixUnaryExpressionSyntax)c.Node;
                    var symbol = c.SemanticModel.GetSymbolInfo(increment.Operand).Symbol;
                    var localSymbol = symbol as ILocalSymbol;
                    var parameterSymbol = symbol as IParameterSymbol;

                    if (localSymbol == null &&
                        (parameterSymbol == null || parameterSymbol.RefKind != RefKind.None))
                    {
                        return;
                    }

                    var operatorText = increment.OperatorToken.IsKind(SyntaxKind.PlusPlusToken)
                        ? "increment"
                        : "decrement";

                    if (increment.Parent is ReturnStatementSyntax)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, increment.GetLocation(), operatorText));
                        return;
                    }
                    if (increment.Parent is ArrowExpressionClauseSyntax)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, increment.GetLocation(), operatorText));
                        return;
                    }

                    var assignment = increment.Parent as AssignmentExpressionSyntax;
                    if (assignment != null &&
                        assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                        assignment.Right == increment &&
                        EquivalenceChecker.AreEquivalent(assignment.Left, increment.Operand))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, increment.GetLocation(), operatorText));
                    }
                },
                SyntaxKind.PostIncrementExpression,
                SyntaxKind.PostDecrementExpression);
        }
    }
}
