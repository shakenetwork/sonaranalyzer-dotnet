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

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious, Tag.Cwe, Tag.Misra)]
    public class AssignmentInsideSubExpression : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1121";
        internal const string Title = "Assignments should not be made from within sub-expressions";
        internal const string Description =
            "Assignments within sub-expressions are hard to spot and therefore make the code less readable. " +
            "It is also a common mistake to write \"=\" when \"==\" was meant. Ideally, expressions should not" +
            "have side-effects. Assignments inside lambda and delegate expressions are allowed.";
        internal const string MessageFormat = "Extract the assignment of \"{0}\" from this expression.";
        internal const string Category = SonarLint.Common.Category.Reliability;
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
                    var assignment = (AssignmentExpressionSyntax) c.Node;

                    if (IsInSubExpression(assignment) ||
                        IsInCondition(assignment))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, assignment.OperatorToken.GetLocation(),
                            assignment.Left.ToString()));
                    }
                },
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
                SyntaxKind.RightShiftAssignmentExpression);
        }

        private static bool IsInSubExpression(SyntaxNode node)
        {
            var expression = node.Parent.FirstAncestorOrSelf<ExpressionSyntax>(ancestor => ancestor != null);

            return expression != null &&
                   !AllowedParentExpressionKinds.Contains(expression.Kind());
        }

        private static bool IsInCondition(SyntaxNode node)
        {
            var ifStatement = node.Parent.FirstAncestorOrSelf<IfStatementSyntax>(ancestor => ancestor != null);
            if (ifStatement != null)
            {
                return ifStatement.Condition == node;
            }

            var forStatement = node.Parent.FirstAncestorOrSelf<ForStatementSyntax>(ancestor => ancestor != null);
            if (forStatement != null)
            {
                return forStatement.Condition == node;
            }

            return false;
        }

        private static readonly IEnumerable<SyntaxKind> AllowedParentExpressionKinds = new[]
        {
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.AnonymousMethodExpression,
            SyntaxKind.ObjectInitializerExpression
        };
    }
}
