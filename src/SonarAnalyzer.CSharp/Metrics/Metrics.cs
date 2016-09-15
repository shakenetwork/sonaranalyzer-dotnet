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


using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SonarAnalyzer.Common.CSharp
{
    public class Metrics : MetricsBase
    {
        public Metrics(SyntaxTree tree) : base(tree)
        {
            var root = tree.GetRoot();
            if (root.Language != LanguageNames.CSharp)
            {
                throw new ArgumentException(InitalizationErrorTextPattern, nameof(tree));
            }
        }
        protected override Func<string, bool> HasValidCommentContent => line => line.Any(char.IsLetter);
        protected override Func<SyntaxToken, bool> IsEndOfFile => token => token.IsKind(SyntaxKind.EndOfFileToken);
        protected override Func<SyntaxToken, bool> IsNoneToken => token => token.IsKind(SyntaxKind.None);
        protected override Func<SyntaxTrivia, bool> IsCommentTrivia => trivia => TriviaKinds.Contains(trivia.Kind());
        protected override Func<SyntaxNode, bool> IsClass => node => ClassKinds.Contains(node.Kind());
        protected override Func<SyntaxNode, bool> IsAccessor => node => AccessorKinds.Contains(node.Kind());
        protected override Func<SyntaxNode, bool> IsStatement => node => node is StatementSyntax && !node.IsKind(SyntaxKind.Block);
        protected override Func<SyntaxNode, bool> IsFunction => node => FunctionKinds.Contains(node.Kind());
        protected override Func<SyntaxNode, bool> IsFunctionWithBody => node =>
            IsFunction(node) &&
            node.ChildNodes().Any(c => c.IsKind(SyntaxKind.Block));

        protected override IEnumerable<SyntaxNode> PublicApiNodes
        {
            get
            {
                var root = tree.GetRoot();
                var publicNodes = ImmutableArray.CreateBuilder<SyntaxNode>();
                var toVisit = new Stack<SyntaxNode>();

                var members = root.ChildNodes()
                    .Where(childNode => childNode is MemberDeclarationSyntax);
                foreach (var member in members)
                {
                    toVisit.Push(member);
                }

                while (toVisit.Any())
                {
                    var member = toVisit.Pop();

                    var isPublic = member.ChildTokens().Any(t => t.IsKind(SyntaxKind.PublicKeyword));
                    if (isPublic)
                    {
                        publicNodes.Add(member);
                    }

                    if (!isPublic &&
                        !member.IsKind(SyntaxKind.NamespaceDeclaration))
                    {
                        continue;
                    }

                    members = member.ChildNodes()
                        .Where(childNode => childNode is MemberDeclarationSyntax);
                    foreach (var child in members)
                    {
                        toVisit.Push(child);
                    }
                }

                return publicNodes.ToImmutable();
            }
        }

        public override int GetComplexity(SyntaxNode node)
        {
            return node
                    .DescendantNodesAndSelf()
                    .Count(
                        n =>
                            // TODO What about the null coalescing operator?
                            // TODO why differentiate between expression bodied and block bodied methods?
                            ComplexityIncreasingKinds.Contains(n.Kind()) ||
                            IsReturnButNotLast(n) ||
                            IsFunctionWithBody(n));
        }
        private bool IsReturnButNotLast(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.ReturnStatement) && !IsLastStatement(node);
        }
        private bool IsLastStatement(SyntaxNode node)
        {
            var nextToken = node.GetLastToken().GetNextToken();
            return nextToken.Parent.IsKind(SyntaxKind.Block) &&
                IsFunction(nextToken.Parent.Parent);
        }

        private static readonly SyntaxKind[] TriviaKinds =
        {
            SyntaxKind.SingleLineCommentTrivia,
            SyntaxKind.MultiLineCommentTrivia,
            SyntaxKind.SingleLineDocumentationCommentTrivia,
            SyntaxKind.MultiLineDocumentationCommentTrivia
        };
        private static readonly SyntaxKind[] ClassKinds =
        {
            SyntaxKind.ClassDeclaration
        };
        private static readonly SyntaxKind[] AccessorKinds =
        {
            SyntaxKind.GetAccessorDeclaration,
            SyntaxKind.SetAccessorDeclaration,
            SyntaxKind.AddAccessorDeclaration,
            SyntaxKind.RemoveAccessorDeclaration
        };
        private static readonly SyntaxKind[] FunctionKinds =
        {
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DestructorDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.GetAccessorDeclaration,
            SyntaxKind.SetAccessorDeclaration,
            SyntaxKind.AddAccessorDeclaration,
            SyntaxKind.RemoveAccessorDeclaration
        };
        private static readonly SyntaxKind[] ComplexityIncreasingKinds =
        {
            SyntaxKind.IfStatement,
            SyntaxKind.SwitchStatement,
            SyntaxKind.LabeledStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.LogicalAndExpression,
            SyntaxKind.LogicalOrExpression,
            SyntaxKind.CaseSwitchLabel
        };
    }
}
