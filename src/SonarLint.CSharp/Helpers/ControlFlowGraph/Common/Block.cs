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

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SonarLint.Helpers.Cfg.Common
{
    /// <summary>
    /// Basic building blocks of a Control Flow Graph (<see cref="IControlFlowGraph"/>).
    /// Holds a list of instructions which have no jumps between them.
    /// </summary>
    public class Block
    {
        // Protected to allow extending and mocking
        protected Block() { }

        public virtual IReadOnlyList<SyntaxNode> Instructions => ReversedInstructions.Reverse().ToImmutableArray();

        public virtual IReadOnlyCollection<Block> PredecessorBlocks => EditablePredecessorBlocks.ToImmutableHashSet();

        public virtual IReadOnlyList<Block> SuccessorBlocks { get; } = ImmutableArray.Create<Block>();

        internal IList<SyntaxNode> ReversedInstructions { get; } = new List<SyntaxNode>();

        internal ISet<Block> EditablePredecessorBlocks { get; } = new HashSet<Block>();

        internal virtual Block GetPossibleNonEmptySuccessorBlock()
        {
            return this;
        }

        internal virtual void ReplaceSuccessors(Dictionary<Block, Block> replacementMapping) { }
    }
}
