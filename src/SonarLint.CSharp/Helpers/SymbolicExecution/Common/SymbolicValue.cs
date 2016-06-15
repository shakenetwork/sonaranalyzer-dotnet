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

        public /* for testing */ SymbolicValue()
            : this(null)
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
    }
}
