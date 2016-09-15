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

namespace SonarAnalyzer.Helpers.FlowAnalysis.Common
{
    public abstract class BinaryRelationship
    {
        internal SymbolicValue LeftOperand { get; }
        internal SymbolicValue RightOperand { get; }

        protected BinaryRelationship(SymbolicValue leftOperand, SymbolicValue rightOperand)
        {
            LeftOperand = leftOperand;
            RightOperand = rightOperand;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return Equals(obj as BinaryRelationship);
        }

        private bool Equals(BinaryRelationship other)
        {
            if (other == null ||
                GetType() != other.GetType())
            {
                return false;
            }

            return LeftOperand.Equals(other.LeftOperand) && RightOperand.Equals(other.RightOperand);
        }

        public override int GetHashCode()
        {
            var hash = 19;
            hash = hash * 31 + GetType().GetHashCode();
            hash = hash * 31 + LeftOperand.GetHashCode();
            hash = hash * 31 + RightOperand.GetHashCode();
            return hash;
        }

        internal abstract bool IsContradicting(IEnumerable<BinaryRelationship> relationships);
        public abstract BinaryRelationship Negate();

        protected bool AreOperandsMatching(BinaryRelationship rel2)
        {
            return LeftOperand.Equals(rel2.LeftOperand) && RightOperand.Equals(rel2.RightOperand) ||
                RightOperand.Equals(rel2.LeftOperand) && LeftOperand.Equals(rel2.RightOperand);
        }
    }
}
