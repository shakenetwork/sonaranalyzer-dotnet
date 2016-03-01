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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("30min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class DelegateSubtraction : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3172";
        internal const string Title = "Delegates should not be subtracted";
        internal const string Description =
            "In C#, delegates can be added together to chain their execution, and subtracted to remove their execution from the chain. " +
            "Subtracting a chain of delegates from another one might yield unexpected results as shown hereunder - and is likely to be a bug.";
        internal const string MessageFormat = "Review this subtraction of a chain of delegates: it may not work as you expect.";
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
                    var assignment = (AssignmentExpressionSyntax)c.Node;
                    if (!IsDelegateSubtraction(assignment, c.SemanticModel) ||
                        ExpressionIsSimple(assignment.Right))
                    {
                        return;
                    }

                    c.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation()));
                },
                SyntaxKind.SubtractAssignmentExpression);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var binary = (BinaryExpressionSyntax)c.Node;
                    if (!IsDelegateSubtraction(binary, c.SemanticModel) ||
                        !IsTopLevelSubtraction(binary))
                    {
                        return;
                    }

                    if (!BinaryIsValidSubstraction(binary))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, binary.GetLocation()));
                    }
                },
                SyntaxKind.SubtractExpression);
        }

        private static bool BinaryIsValidSubstraction(BinaryExpressionSyntax subtraction)
        {
            var currentSubtraction = subtraction;

            while (currentSubtraction != null &&
                   currentSubtraction.IsKind(SyntaxKind.SubtractExpression))
            {
                if (!ExpressionIsSimple(currentSubtraction.Right))
                {
                    return false;
                }

                currentSubtraction = currentSubtraction.Left as BinaryExpressionSyntax;
            }
            return true;
        }

        private static bool IsTopLevelSubtraction(BinaryExpressionSyntax subtraction)
        {
            var parent = subtraction.Parent as BinaryExpressionSyntax;
            return parent == null || !parent.IsKind(SyntaxKind.SubtractExpression);
        }

        private static bool IsDelegateSubtraction(SyntaxNode node, SemanticModel semanticModel)
        {
            var subtractMethod = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
            return subtractMethod != null &&
                subtractMethod.ReceiverType.Is(TypeKind.Delegate);
        }

        private static bool ExpressionIsSimple(ExpressionSyntax expression)
        {
            return expression is IdentifierNameSyntax || expression is MemberAccessExpressionSyntax;
        }
    }
}
