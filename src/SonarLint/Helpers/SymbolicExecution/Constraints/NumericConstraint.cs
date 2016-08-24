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
using System.Collections.Immutable;
using System.Linq;

namespace SonarLint.Helpers.FlowAnalysis.Common
{
    internal sealed class NumericConstraint : SymbolicValueConstraint
    {
        internal readonly DistinctIntervalSet intervalSet;

        public NumericConstraint(int value)
            : this(new Interval(value, value))
        { }

        public NumericConstraint(Interval interval)
            : this(new DistinctIntervalSet(interval))
        { }

        public NumericConstraint(DistinctIntervalSet intervalSet)
        {
            this.intervalSet = intervalSet;
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

            return numConstraint.intervalSet.Contains(this.intervalSet);
        }

        public override SymbolicValueConstraint OppositeForLogicalNot
        {
            get
            {
                var complements = new DistinctIntervalSet(new Interval(int.MinValue, int.MaxValue));
                foreach (var interval in this.intervalSet.intervals)
                {
                    var complement = interval.Complement;
                    complements = complements.Intersect(complement);
                }

                return new NumericConstraint(complements);
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

            return intervalSet.Equals(other.intervalSet);
        }

        public override int GetHashCode()
        {
            return intervalSet.GetHashCode();
        }

    }

    public class DistinctIntervalSet
    {
        internal readonly IImmutableSet<Interval> intervals;

        public DistinctIntervalSet()
            : this(Enumerable.Empty<Interval>())
        { }

        public DistinctIntervalSet(params Interval[] intervals)
            : this((IEnumerable<Interval>)(intervals ?? new Interval[0]))
        { }

        public DistinctIntervalSet(IEnumerable<Interval> intervals)
        {
            this.intervals = Combine(intervals.ToImmutableHashSet());
        }

        private static IImmutableSet<Interval> Combine(IImmutableSet<Interval> intervals)
        {
            IImmutableSet<Interval> distinctIntervals = ImmutableHashSet<Interval>.Empty;
            foreach (var interval in intervals)
            {
                distinctIntervals = Combine(distinctIntervals, interval);
            }
            return distinctIntervals;
        }

        private static IImmutableSet<Interval> Combine(IEnumerable<Interval> intervals, Interval interval)
        {
            foreach (var i in intervals)
            {
                Interval unioned;
                if (i.TryUnion(interval, out unioned))
                {
                    var intervalList = intervals.ToList();
                    intervalList.Remove(i);
                    return Combine(intervalList, unioned);
                }
            }

            return intervals.ToImmutableHashSet().Add(interval);
        }

        public DistinctIntervalSet Intersect(Interval interval)
        {
            return new DistinctIntervalSet(intervals
                .Where(i => i.Intersects(interval))
                .Select(i => i.Intersect(interval))
                .ToImmutableHashSet());
        }

        public DistinctIntervalSet Intersect(DistinctIntervalSet intervalSet)
        {
            return new DistinctIntervalSet(intervalSet.intervals
                .SelectMany(i => Intersect(i).intervals)
                .ToImmutableHashSet());
        }

        public bool Contains(DistinctIntervalSet other)
        {
            return Intersect(other).Equals(other);
        }

        public bool IsEmpty => !intervals.Any();
        public int Max => intervals.Max(i => i.max);
        public int Min => intervals.Min(i => i.min);

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return Equals(obj as DistinctIntervalSet);
        }

        private bool Equals(DistinctIntervalSet other)
        {
            if (other == null)
            {
                return false;
            }

            return intervals.SetEquals(other.intervals);
        }

        public override int GetHashCode()
        {
            var hash = 19;
            hash = hash * 31 + intervals.Count.GetHashCode();

            foreach (var interval in intervals)
            {
                hash = hash * 31 + interval.GetHashCode();
            }

            return hash;
        }
    }

    /// <summary>
    /// Closed interval
    /// </summary>
    public struct Interval
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
                throw new ArgumentException(nameof(other));
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

            result = new Interval();
            return false;
        }

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
    }
}
