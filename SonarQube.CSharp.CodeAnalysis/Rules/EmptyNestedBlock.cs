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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug")]
    public class EmptyNestedBlock : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S108";
        internal const string Description = "Nested blocks of code should not be left empty";
        internal const string MessageFormat = "Either remove or fill this block of code.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major; 
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS108");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    if (IsEmpty(c.Node))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation()));
                    }
                },
                SyntaxKind.Block,
                SyntaxKind.SwitchStatement);
        }

        private static bool IsEmpty(SyntaxNode node)
        {
            var switchNode = node as SwitchStatementSyntax;
            var blockNode = node as BlockSyntax;

            return (switchNode != null && IsEmpty(switchNode)) ||
                (blockNode != null && IsNestedAndEmpty(blockNode));
        }

        private static bool IsEmpty(SwitchStatementSyntax node)
        {
            return !node.Sections.Any();
        }

        private static bool IsNestedAndEmpty(BlockSyntax node)
        {
            return IsNested(node) && IsEmpty(node);
        }

        private static bool IsNested(BlockSyntax node)
        {
            return !AllowedContainerKinds.Contains(node.Parent.Kind());
        }

        private static IEnumerable<SyntaxKind> AllowedContainerKinds
        {
            get
            {
                return new[]
                {
                    SyntaxKind.ConstructorDeclaration,
                    SyntaxKind.DestructorDeclaration, 
                    SyntaxKind.MethodDeclaration,
                    SyntaxKind.SimpleLambdaExpression,
                    SyntaxKind.ParenthesizedLambdaExpression,
                    SyntaxKind.AnonymousMethodExpression
                };
            }
        }

        private static bool IsEmpty(BlockSyntax node)
        {
            return !node.Statements.Any() && !ContainsComment(node);
        }

        private static bool ContainsComment(BlockSyntax node)
        {
            return ContainsComment(node.OpenBraceToken.TrailingTrivia) || ContainsComment(node.CloseBraceToken.LeadingTrivia);
        }

        private static bool ContainsComment(SyntaxTriviaList trivias)
        {
            return trivias.Any(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia));
        }
    }
}
