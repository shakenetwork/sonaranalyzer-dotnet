/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2017 SonarSource SA
 * mailto: contact AT sonarsource DOT com
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
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using Microsoft.CodeAnalysis;

namespace SonarAnalyzer.Common
{
    public class SyntaxNodeWithSymbol<TSyntaxNode>
        where TSyntaxNode : SyntaxNode
    {

        public SyntaxNodeWithSymbol(TSyntaxNode syntax, ISymbol symbol)
        {
            Syntax = syntax;
            Symbol = symbol;
        }

        public TSyntaxNode Syntax { get; }
        public ISymbol Symbol { get; }
    }

    public static class SyntaxNodeWithSymbolHelper
    {
        public static SyntaxNodeWithSymbol<TSyntaxNode> ToSyntaxWithSymbol<TSyntaxNode>(this TSyntaxNode syntax, ISymbol symbol)
            where TSyntaxNode : SyntaxNode
        {
            return new SyntaxNodeWithSymbol<TSyntaxNode>(syntax, symbol);
        }

        public static SyntaxNodeWithSymbol<TSyntaxNode> ToSyntaxWithSymbol<TSyntaxNode>(this ISymbol symbol, TSyntaxNode syntax)
            where TSyntaxNode : SyntaxNode
        {
            return new SyntaxNodeWithSymbol<TSyntaxNode>(syntax, symbol);
        }
    }
}