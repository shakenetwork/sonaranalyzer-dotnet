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

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace SonarAnalyzer.Helpers
{
    internal static class SyntaxHelper
    {
        public static readonly ExpressionSyntax NullLiteralExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
        public static readonly ExpressionSyntax FalseLiteralExpression = SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);
        public static readonly ExpressionSyntax TrueLiteralExpression = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);

        public static bool HasExactlyNArguments(this InvocationExpressionSyntax invocation, int count)
        {
            return invocation != null &&
                invocation.ArgumentList != null &&
                invocation.ArgumentList.Arguments.Count == count;
        }

        public static ExpressionSyntax RemoveParentheses(this ExpressionSyntax expression)
        {
            var currentExpression = expression;
            var parentheses = expression as ParenthesizedExpressionSyntax;
            while (parentheses != null)
            {
                currentExpression = parentheses.Expression;
                parentheses = currentExpression as ParenthesizedExpressionSyntax;
            }
            return currentExpression;
        }

        public static ExpressionSyntax GetSelfOrTopParenthesizedExpression(this ExpressionSyntax node)
        {
            var current = node;
            var parent = current.Parent as ParenthesizedExpressionSyntax;
            while (parent != null)
            {
                current = parent;
                parent = current.Parent as ParenthesizedExpressionSyntax;
            }
            return current;
        }

        public static bool TryGetAttribute(this SyntaxList<AttributeListSyntax> attributeLists, KnownType attributeKnownType,
            SemanticModel semanticModel, out AttributeSyntax searchedAttribute)
        {
            searchedAttribute = null;

            if (!attributeLists.Any())
            {
                return false;
            }

            foreach (var attribute in attributeLists.SelectMany(attributeList => attributeList.Attributes))
            {
                var attributeType = semanticModel.GetTypeInfo(attribute).Type;

                if (attributeType.Is(attributeKnownType))
                {
                    searchedAttribute = attribute;
                    return true;
                }
            }

            return false;
        }

        public static bool IsOnThis(this ExpressionSyntax expression)
        {
            if (expression is NameSyntax)
            {
                return true;
            }

            var memberAccess = expression as MemberAccessExpressionSyntax;
            if (memberAccess != null &&
                memberAccess.Expression.IsKind(SyntaxKind.ThisExpression))
            {
                return true;
            }

            var conditionalAccess = expression as ConditionalAccessExpressionSyntax;
            if (conditionalAccess != null &&
                conditionalAccess.Expression.IsKind(SyntaxKind.ThisExpression))
            {
                return true;
            }

            return false;
        }

        public static bool IsInNameofCall(this ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var argumentList = (expression.Parent as ArgumentSyntax)?.Parent as ArgumentListSyntax;
            var nameofCall = argumentList?.Parent as InvocationExpressionSyntax;

            if (nameofCall == null)
            {
                return false;
            }

            var calledSymbol = semanticModel.GetSymbolInfo(nameofCall).Symbol as IMethodSymbol;
            if (calledSymbol != null)
            {
                return false;
            }

            var nameofIdentifier = (nameofCall?.Expression as IdentifierNameSyntax)?.Identifier;
            return nameofIdentifier.HasValue &&
                (nameofIdentifier.Value.ToString() == SyntaxFacts.GetText(SyntaxKind.NameOfKeyword));
        }
    }
}
