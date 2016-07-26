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

using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarLint.Helpers.FlowAnalysis.Common
{
    public class NotEqualsSymbolicValue : RelationalSymbolicValue
    {
        public NotEqualsSymbolicValue(SymbolicValue leftOperand, SymbolicValue rightOperand)
            : base(leftOperand, rightOperand)
        {
        }

        public override IEnumerable<ProgramState> TrySetConstraint(SymbolicValueConstraint constraint, ProgramState currentProgramState)
        {
            var boolConstraint = constraint as BoolConstraint;
            if (boolConstraint == null)
            {
                return new[] { currentProgramState };
            }

            SymbolicValueConstraint leftConstraint;
            var leftHasConstraint = leftOperand.TryGetConstraint(currentProgramState, out leftConstraint);
            SymbolicValueConstraint rightConstraint;
            var rightHasConstraint = rightOperand.TryGetConstraint(currentProgramState, out rightConstraint);

            var relationship = boolConstraint == BoolConstraint.False
                ? (BinaryRelationship)new EqualsRelationship(leftOperand, rightOperand)
                : new NotEqualsRelationship(leftOperand, rightOperand);

            var newProgramState = currentProgramState.TrySetRelationship(relationship);
            if (newProgramState == null)
            {
                return Enumerable.Empty<ProgramState>();
            }

            if (!rightHasConstraint && !leftHasConstraint)
            {
                return new[] { newProgramState };
            }

            if (boolConstraint == BoolConstraint.False)
            {
                return rightOperand.TrySetConstraint(leftConstraint, newProgramState)
                    .SelectMany(ps => leftOperand.TrySetConstraint(rightConstraint, ps));
            }

            return rightOperand.TrySetConstraint(leftConstraint?.OppositeForLogicalNot, newProgramState)
                .SelectMany(ps => leftOperand.TrySetConstraint(rightConstraint?.OppositeForLogicalNot, ps));
        }

        public override string ToString()
        {
            return leftOperand + " != " + rightOperand;
        }
    }
}
