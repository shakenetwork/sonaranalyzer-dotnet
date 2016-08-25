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
    public class Interval
    {
        internal readonly int min;
        internal readonly int max;

        public Interval(int min, int max)
        {
            if (min > max)
            {
                this.min = max;
                this.max = min;
            }
            else
            {
                this.min = min;
                this.max = max;
            }
        }

        public override string ToString()
        {
            return $"[{min}, {max}]";
        }

        private bool Contains(int i)
        {
            return min <= i && max >= i;
        }

        public bool Intersects(Interval other)
        {
            return Contains(other.min) ||
                Contains(other.max) ||
                other.Contains(min);
        }

        public Interval Intersect(Interval other)
        {
            if (!Intersects(other))
            {
                return null;
            }

            return new Interval(Math.Max(min, other.min), Math.Min(max, other.max));
        }

        public bool TryUnion(Interval other, out Interval result)
        {
            if (Intersects(other))
            {
                result = new Interval(Math.Min(min, other.min), Math.Max(max, other.max));
                return true;
            }

            if (max != int.MaxValue && max + 1 == other.min)
            {
                result = new Interval(min, other.max);
                return true;
            }

            if (other.max != int.MaxValue && other.max + 1 == min)
            {
                result = new Interval(other.min, max);
                return true;
            }

            result = null;
            return false;
        }

        public bool IsSingleValue => min == max;

        public DistinctIntervalSet Complement
        {
            get
            {
                if (min == int.MinValue && max == int.MaxValue)
                {
                    return new DistinctIntervalSet();
                }

                if (min == int.MinValue)
                {
                    return new DistinctIntervalSet(new Interval(max + 1, int.MaxValue));
                }
                if (max == int.MaxValue)
                {
                    return new DistinctIntervalSet(new Interval(int.MinValue, min - 1));
                }

                return new DistinctIntervalSet(
                    new Interval(int.MinValue, min - 1),
                    new Interval(max + 1, int.MaxValue));
            }
        }

        public Interval Increment
        {
            get
            {
                return new Interval(
                    min == int.MaxValue ? int.MaxValue : min + 1,
                    max == int.MaxValue ? int.MaxValue : max + 1);
            }
        }
        public Interval Decrement
        {
            get
            {
                return new Interval(
                    min == int.MinValue ? int.MinValue : min - 1,
                    max == int.MinValue ? int.MinValue : max - 1);
            }
        }
    }
}
