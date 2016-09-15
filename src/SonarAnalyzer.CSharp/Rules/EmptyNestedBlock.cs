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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious)]
    public class EmptyNestedBlock : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S108";
        internal const string Title = "Nested blocks of code should not be left empty";
        internal const string Description =
            "Most of the time a block of code is empty when a piece of code is really missing. " +
            "So such empty block must be either filled or removed.";
        internal const string MessageFormat = "Either remove or fill this block of code.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
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
