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
using System.Collections.Generic;
using System.Collections.Immutable;
using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace SonarAnalyzer.Runner
{
    public static class TokenHelper
    {
        // From Roslyn (http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis.CSharp.Workspaces/LanguageServices/CSharpSyntaxFactsService.cs,1453)
        public static SyntaxNode GetBindableParent(this SyntaxToken token)
        {
            return token.Language == LanguageNames.CSharp
                ? GetBindableParentCs(token)
                : GetBindableParentVb(token);
        }

        public static SyntaxNode GetBindableParentVb(SyntaxToken token)
        {
            var node = token.Parent;
            while (node != null)
            {
                var parent = node.Parent;

                // If this node is on the left side of a member access expression, don't ascend
                // further or we'll end up binding to something else.
                var memberAccess = parent as VB.Syntax.MemberAccessExpressionSyntax;
                if (memberAccess != null)
                {
                    if (memberAccess.Expression == node)
                    {
                        break;
                    }
                }

                // If this node is on the left side of a qualified name, don't ascend
                // further or we'll end up binding to something else.
                var qualifiedName = parent as VB.Syntax.QualifiedNameSyntax;
                if (qualifiedName != null)
                {
                    if (qualifiedName.Left == node)
                    {
                        break;
                    }
                }

                // If this node is the type of an object creation expression, return the
                // object creation expression.
                var objectCreation = parent as VB.Syntax.ObjectCreationExpressionSyntax;
                if (objectCreation != null)
                {
                    if (objectCreation.Type == node)
                    {
                        node = parent;
                        break;
                    }
                }

                // The inside of an interpolated string is treated as its own token so we
                // need to force navigation to the parent expression syntax.
                if (node is VB.Syntax.InterpolatedStringTextSyntax && parent is VB.Syntax.InterpolatedStringExpressionSyntax)
                {
                    node = parent;
                    break;
                }

                // If this node is not parented by a name, we're done.
                var name = parent as VB.Syntax.NameSyntax;
                if (name == null)
                {
                    break;
                }

                node = parent;
            }

            return node;
        }

        private static SyntaxNode GetBindableParentCs(SyntaxToken token)
        {
            var node = token.Parent;
            while (node != null)
            {
                var parent = node.Parent;

                // If this node is on the left side of a member access expression, don't ascend
                // further or we'll end up binding to something else.
                var memberAccess = parent as CS.Syntax.MemberAccessExpressionSyntax;
                if (memberAccess != null &&
                    memberAccess.Expression == node)
                {
                    break;
                }

                // If this node is on the left side of a qualified name, don't ascend
                // further or we'll end up binding to something else.
                var qualifiedName = parent as CS.Syntax.QualifiedNameSyntax;
                if (qualifiedName != null &&
                    qualifiedName.Left == node)
                {
                    break;
                }

                // If this node is on the left side of a alias-qualified name, don't ascend
                // further or we'll end up binding to something else.
                var aliasQualifiedName = parent as CS.Syntax.AliasQualifiedNameSyntax;
                if (aliasQualifiedName != null &&
                    aliasQualifiedName.Alias == node)
                {
                    break;
                }

                // If this node is the type of an object creation expression, return the
                // object creation expression.
                var objectCreation = parent as CS.Syntax.ObjectCreationExpressionSyntax;
                if (objectCreation != null &&
                    objectCreation.Type == node)
                {
                    node = parent;
                    break;
                }

                // The inside of an interpolated string is treated as its own token so we
                // need to force navigation to the parent expression syntax.
                if (node is CS.Syntax.InterpolatedStringTextSyntax && parent is CS.Syntax.InterpolatedStringExpressionSyntax)
                {
                    node = parent;
                    break;
                }

                // If this node is not parented by a name, we're done.
                var name = parent as CS.Syntax.NameSyntax;
                if (name == null)
                {
                    break;
                }

                node = parent;
            }

            return node;
        }

        public static bool IsUsingDirective(this SyntaxNode node)
        {
            return node is CS.Syntax.UsingDirectiveSyntax ||
                node is VB.Syntax.ImportsStatementSyntax;
        }

        public static string GetCpdValue(this SyntaxToken token)
        {
            if (NumericLiteralKinds.Contains(token.RawKind))
            {
                return "$num";
            }

            if (StringLiteralKinds.Contains(token.RawKind))
            {
                return "$str";
            }

            if (CharacterLiteralKinds.Contains(token.RawKind))
            {
                return "$char";
            }

            return token.Text;
        }

        private static readonly ISet<int> NumericLiteralKinds = ImmutableHashSet.Create(
            (int)CS.SyntaxKind.NumericLiteralToken,

            (int)VB.SyntaxKind.DecimalLiteralToken,
            (int)VB.SyntaxKind.FloatingLiteralToken,
            (int)VB.SyntaxKind.IntegerLiteralToken);

        private static readonly ISet<int> StringLiteralKinds = ImmutableHashSet.Create(
            (int)CS.SyntaxKind.StringLiteralToken,

            (int)VB.SyntaxKind.StringLiteralToken);

        private static readonly ISet<int> CharacterLiteralKinds = ImmutableHashSet.Create(
            (int)CS.SyntaxKind.CharacterLiteralToken,

            (int)VB.SyntaxKind.CharacterLiteralToken);
    }
}
