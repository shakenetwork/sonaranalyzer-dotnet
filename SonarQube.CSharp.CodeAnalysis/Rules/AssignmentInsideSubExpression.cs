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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug", "cwe", "misra")]
    public class AssignmentInsideSubExpression : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1121";
        internal const string Description = "Assignments should not be made from within sub-expressions";
        internal const string MessageFormat = "Extract the assignment of \"{0}\" from this expression.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS1121");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    if (IsInSubExpression(c.Node))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation(), c.Node.ChildNodes().First().ToString()));
                        return;
                    }

                    if (IsInCondition(c.Node))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation(), c.Node.ChildNodes().First().ToString()));
                        return;
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

        private static IEnumerable<SyntaxKind> AllowedParentExpressionKinds
        {
            get
            {
                return new[]
                {
                    SyntaxKind.ParenthesizedLambdaExpression,
                    SyntaxKind.SimpleLambdaExpression,
                    SyntaxKind.AnonymousMethodExpression,
                    SyntaxKind.ObjectInitializerExpression
                };
            }
        }
    }
}
