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

namespace SonarLint.Helpers.FlowAnalysis.Common
{
    public class SymbolicValue
    {
        public static readonly SymbolicValue True = new BoolLiteralSymbolicValue(true);
        public static readonly SymbolicValue False = new BoolLiteralSymbolicValue(false);
        public static readonly SymbolicValue Null = new NullSymbolicValue();

        private class BoolLiteralSymbolicValue : SymbolicValue
        {
            internal BoolLiteralSymbolicValue(bool value)
                : base(value)
            {
            }
        }

        private class NullSymbolicValue : SymbolicValue
        {
            internal NullSymbolicValue()
                : base(new object())
            {
            }
            public override string ToString()
            {
                return "SymbolicValue NULL";
            }
        }

        private readonly object identifier;

        internal SymbolicValue()
            : this(new object())
        {
        }

        private SymbolicValue(object identifier)
        {
            this.identifier = identifier;
        }

        public override string ToString()
        {
            if (identifier != null)
            {
                return "SymbolicValue " + identifier;
            }

            return base.ToString();
        }

        internal ProgramState SetConstraint(SymbolicValueConstraint constraint, ProgramState programState)
        {
            return new ProgramState(
                programState.Values,
                programState.Constraints.SetItem(this, constraint),
                programState.ProgramPointVisitCounts,
                programState.ExpressionStack);
        }

        public virtual bool TrySetConstraint(SymbolicValueConstraint constraint, ProgramState currentProgramState, out ProgramState newProgramState)
        {
            newProgramState = null;

            SymbolicValueConstraint oldConstraint;
            if (!currentProgramState.Constraints.TryGetValue(this, out oldConstraint))
            {
                newProgramState = SetConstraint(constraint, currentProgramState);
                return true;
            }

            var boolConstraint = constraint as BoolConstraint;
            if (boolConstraint != null)
            {
                return TrySetConstraint(boolConstraint, oldConstraint, currentProgramState, out newProgramState);
            }

            var objectConstraint = constraint as ObjectConstraint;
            if (objectConstraint != null)
            {
                return TrySetConstraint(objectConstraint, oldConstraint, currentProgramState, out newProgramState);
            }

            throw new NotSupportedException($"Neither {nameof(BoolConstraint)}, nor {nameof(ObjectConstraint)}");
        }

        private bool TrySetConstraint(BoolConstraint boolConstraint, SymbolicValueConstraint oldConstraint,
            ProgramState currentProgramState, out ProgramState newProgramState)
        {
            newProgramState = null;

            if (oldConstraint == ObjectConstraint.Null)
            {
                // It was null, and now it should be true or false
                return false;
            }

            var oldBoolConstraint = oldConstraint as BoolConstraint;
            if (oldBoolConstraint != null &&
                oldBoolConstraint != boolConstraint)
            {
                return false;
            }

            // Either same bool constraint, or previously not null, and now a bool constraint
            newProgramState = SetConstraint(boolConstraint, currentProgramState);
            return true;
        }

        private bool TrySetConstraint(ObjectConstraint objectConstraint, SymbolicValueConstraint oldConstraint,
            ProgramState currentProgramState, out ProgramState newProgramState)
        {
            newProgramState = null;

            var oldBoolConstraint = oldConstraint as BoolConstraint;
            if (oldBoolConstraint != null)
            {
                if (objectConstraint == ObjectConstraint.Null)
                {
                    return false;
                }

                newProgramState = currentProgramState;
                return true;
            }

            var oldObjectConstraint = oldConstraint as ObjectConstraint;
            if (oldObjectConstraint != null)
            {
                if (oldObjectConstraint != objectConstraint)
                {
                    return false;
                }

                newProgramState = SetConstraint(objectConstraint, currentProgramState);
                return true;
            }

            throw new NotSupportedException($"Neither {nameof(BoolConstraint)}, nor {nameof(ObjectConstraint)}");
        }
    }
}
