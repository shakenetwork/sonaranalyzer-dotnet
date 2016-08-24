﻿/*
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
    public class NumericSymbolicValue : SymbolicValue
    {
        private readonly int number;

        public NumericSymbolicValue(int number)
        {
            this.number = number;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return Equals(obj as NumericSymbolicValue);
        }

        private bool Equals(NumericSymbolicValue other)
        {
            if (other == null)
            {
                return false;
            }

            return number == other.number;
        }

        public override int GetHashCode()
        {
            var hash = 19;
            hash = hash * 31 + typeof(NumericSymbolicValue).GetHashCode();
            hash = hash * 31 + number.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return $"NUM({number})";
        }
    }
}
