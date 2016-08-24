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

using System.Collections.Generic;
using System.Linq;

namespace SonarLint.Helpers.FlowAnalysis.Common
{
    public class ComparisonSymbolicValue : BinarySymbolicValue
    {
        private readonly ComparisonKind comparisonKind;

        public ComparisonSymbolicValue(ComparisonKind comparisonKind, SymbolicValue leftOperand, SymbolicValue rightOperand)
            : base(leftOperand, rightOperand)
        {
            this.comparisonKind = comparisonKind;
        }

        public override IEnumerable<ProgramState> TrySetConstraint(SymbolicValueConstraint constraint, ProgramState currentProgramState)
        {
            var boolConstraint = constraint as BoolConstraint;
            if (boolConstraint == null)
            {
                return new[] { currentProgramState };
            }

            var relationship = GetRelationship(boolConstraint);

            var newProgramState = currentProgramState.TrySetRelationship(relationship);
            if (newProgramState == null)
            {
                return Enumerable.Empty<ProgramState>();
            }

            var comparison = this;
            if (constraint == BoolConstraint.False)
            {
                var otherComparisonKind = comparisonKind == ComparisonKind.Less
                    ? ComparisonKind.LessOrEqual
                    : ComparisonKind.Less;
                comparison = new ComparisonSymbolicValue(otherComparisonKind, rightOperand, leftOperand);
            }

            IEnumerable<ProgramState> newStates = null;
            SymbolicValueConstraint leftConstraint;
            if (comparison.leftOperand.TryGetConstraint(newProgramState, out leftConstraint))
            {
                var leftNumericConstraint = leftConstraint as NumericConstraint;
                if (leftNumericConstraint != null)
                {
                    var min = leftNumericConstraint.intervalSet.Min;
                    if (comparison.comparisonKind == ComparisonKind.Less)
                    {
                        min++;
                    }
                    var newConstraint = new NumericConstraint(
                        new Interval(min, int.MaxValue));

                    newStates = newStates ?? new[] { newProgramState };
                    newStates = newStates.SelectMany(ns => comparison.rightOperand.TrySetConstraint(newConstraint, ns));
                }
            }

            SymbolicValueConstraint rightConstraint;
            if (comparison.rightOperand.TryGetConstraint(newProgramState, out rightConstraint))
            {
                var rightNumericConstraint = rightConstraint as NumericConstraint;
                if (rightNumericConstraint != null)
                {
                    var max = rightNumericConstraint.intervalSet.Max;
                    if (comparison.comparisonKind == ComparisonKind.Less)
                    {
                        max--;
                    }
                    var newConstraint = new NumericConstraint(
                        new Interval(int.MinValue, max));

                    newStates = newStates ?? new[] { newProgramState };
                    newStates = newStates.SelectMany(ns => comparison.leftOperand.TrySetConstraint(newConstraint, ns));
                }
            }

            return newStates ?? new[] { newProgramState };
        }

        private BinaryRelationship GetRelationship(BoolConstraint boolConstraint)
        {
            var relationship = new ComparisonRelationship(comparisonKind, leftOperand, rightOperand);

            return boolConstraint == BoolConstraint.True
                ? relationship
                : relationship.Negate();
        }

        public override string ToString()
        {
            var op = comparisonKind == ComparisonKind.Less
                ? "<"
                : "<=";
            return $"{op}({leftOperand}, {rightOperand})";
        }
    }
}
