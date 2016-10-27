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

namespace SonarAnalyzer.Helpers.FlowAnalysis.Common
{
    public abstract class EqualsRelationship : BinaryRelationship
    {
        protected EqualsRelationship(SymbolicValue leftOperand, SymbolicValue rightOperand)
            : base(leftOperand, rightOperand)
        {
        }

        protected bool Equals(EqualsRelationship other)
        {
            return other != null && AreOperandsMatching(other);
        }

        public sealed override int GetHashCode()
        {
            var left = LeftOperand.GetHashCode();
            var right = RightOperand.GetHashCode();

            return GetHashCodeMinMaxOrdered(left, right, GetType().GetHashCode());
        }

        internal static int GetHashCodeMinMaxOrdered(int leftHash, int rightHash, int typeHash)
        {
            var min = System.Math.Min(leftHash, rightHash);
            var max = System.Math.Max(leftHash, rightHash);

            var hash = 19;
            hash = hash * 31 + typeHash;
            hash = hash * 31 + min.GetHashCode();
            hash = hash * 31 + max.GetHashCode();
            return hash;
        }

        internal override IEnumerable<BinaryRelationship> GetTransitiveRelationships(ImmutableHashSet<BinaryRelationship> relationships)
        {
            // todo
            return Enumerable.Empty<BinaryRelationship>();
        }

        protected BinaryRelationship GetTransitiveRelationship(BinaryRelationship other, BinaryRelationship operatorfactory)
        {
            if (LeftOperand.Equals(other.LeftOperand))
            {
                return operatorfactory.CreateNewWithOperands(RightOperand, other.RightOperand);
            }
            else if (RightOperand.Equals(other.LeftOperand))
            {
                return operatorfactory.CreateNewWithOperands(LeftOperand, other.RightOperand);
            }
            else if (LeftOperand.Equals(other.RightOperand))
            {
                return operatorfactory.CreateNewWithOperands(other.LeftOperand, RightOperand);
            }
            else if (RightOperand.Equals(other.RightOperand))
            {
                return operatorfactory.CreateNewWithOperands(other.LeftOperand, LeftOperand);
            }
            else
            {
                return null;
            }
        }
    }
}
