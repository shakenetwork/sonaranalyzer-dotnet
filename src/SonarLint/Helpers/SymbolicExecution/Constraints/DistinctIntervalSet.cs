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
using System.Collections.Immutable;
using System.Linq;

namespace SonarLint.Helpers.FlowAnalysis.Common
{
    public class DistinctIntervalSet
    {
        private readonly IImmutableSet<Interval> intervals;

        public DistinctIntervalSet()
            : this(Enumerable.Empty<Interval>())
        { }

        public DistinctIntervalSet(params Interval[] intervals)
            : this((IEnumerable<Interval>)(intervals ?? new Interval[0]))
        { }

        public DistinctIntervalSet(IEnumerable<Interval> intervals)
        {
            this.intervals = MakeDistinctSets(intervals.ToImmutableHashSet());
        }

        private static IImmutableSet<Interval> MakeDistinctSets(IImmutableSet<Interval> intervals)
        {
            IImmutableSet<Interval> distinctIntervals = ImmutableHashSet<Interval>.Empty;
            foreach (var interval in intervals)
            {
                distinctIntervals = MakeDistinctSets(distinctIntervals, interval);
            }
            return distinctIntervals;
        }

        private static IImmutableSet<Interval> MakeDistinctSets(IEnumerable<Interval> intervals, Interval interval)
        {
            foreach (var i in intervals)
            {
                Interval unioned;
                if (i.TryUnion(interval, out unioned))
                {
                    var intervalList = intervals.ToList();
                    intervalList.Remove(i);
                    return MakeDistinctSets(intervalList, unioned);
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

        public bool FullyContains(DistinctIntervalSet other)
        {
            return Intersect(other).Equals(other);
        }

        public bool IsEmpty => !intervals.Any();
        public DistinctIntervalSet SingleValueComplement =>
            intervals.Count == 1 && intervals.First().IsSingleValue
            ? intervals.First().Complement
            : null;
        public int Max => intervals.Max(i => i.max);
        public int Min => intervals.Min(i => i.min);

        public DistinctIntervalSet Increment =>
            new DistinctIntervalSet(intervals.Select(i => i.Increment));

        public DistinctIntervalSet Decrement =>
            new DistinctIntervalSet(intervals.Select(i => i.Decrement));

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
}
