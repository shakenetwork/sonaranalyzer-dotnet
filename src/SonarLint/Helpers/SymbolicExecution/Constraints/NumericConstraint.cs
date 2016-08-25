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

namespace SonarLint.Helpers.FlowAnalysis.Common
{
    internal sealed class NumericConstraint : SymbolicValueConstraint
    {
        internal DistinctIntervalSet IntervalSet { get; }

        public NumericConstraint(int value)
            : this(new Interval(value, value))
        { }

        public NumericConstraint(Interval interval)
            : this(new DistinctIntervalSet(interval))
        { }

        public NumericConstraint(DistinctIntervalSet intervalSet)
        {
            this.IntervalSet = intervalSet;
        }

        internal override bool Implies(SymbolicValueConstraint constraint)
        {
            if (base.Implies(constraint) ||
                constraint.Equals(ObjectConstraint.NotNull))
            {
                return true;
            }

            var numConstraint = constraint as NumericConstraint;
            if (numConstraint == null)
            {
                return false;
            }

            return numConstraint.IntervalSet.FullyContains(this.IntervalSet);
        }

        public override SymbolicValueConstraint OppositeForLogicalNot
        {
            get
            {
                var complement = IntervalSet.SingleValueComplement;
                if (complement == null)
                {
                    return null;
                }

                return new NumericConstraint(complement);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return Equals(obj as NumericConstraint);
        }

        private bool Equals(NumericConstraint other)
        {
            if (other == null)
            {
                return false;
            }

            return IntervalSet.Equals(other.IntervalSet);
        }

        public override int GetHashCode()
        {
            return IntervalSet.GetHashCode();
        }

    }
}
