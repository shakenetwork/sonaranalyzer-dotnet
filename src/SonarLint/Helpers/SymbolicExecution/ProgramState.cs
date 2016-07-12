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

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SonarLint.Helpers.FlowAnalysis.Common
{
    public class ProgramState : IEquatable<ProgramState>
    {
        internal ImmutableDictionary<ISymbol, SymbolicValue> Values { get; }
        internal ImmutableDictionary<SymbolicValue, SymbolicValueConstraint> Constraints { get; }
        internal ImmutableDictionary<ProgramPoint, int> ProgramPointVisitCounts { get; }

        private static ImmutableDictionary<SymbolicValue, SymbolicValueConstraint> InitialConstraints =>
            new Dictionary<SymbolicValue, SymbolicValueConstraint>
            {
                { SymbolicValue.True, BoolConstraint.True },
                { SymbolicValue.False, BoolConstraint.False },
                { SymbolicValue.Null, ObjectConstraint.Null }
            }.ToImmutableDictionary();

        private static readonly ISet<SymbolicValue> ProtectedSymbolicValues = ImmutableHashSet.Create(
            SymbolicValue.True,
            SymbolicValue.False,
            SymbolicValue.Null);

        internal ProgramState()
            : this(ImmutableDictionary<ISymbol, SymbolicValue>.Empty,
                  InitialConstraints,
                  ImmutableDictionary<ProgramPoint, int>.Empty)
        {
        }

        internal ProgramState(ImmutableDictionary<ISymbol, SymbolicValue> values,
            ImmutableDictionary<SymbolicValue, SymbolicValueConstraint> constraints,
            ImmutableDictionary<ProgramPoint, int> programPointVisitCounts)
        {
            Values = values;
            Constraints = constraints;
            ProgramPointVisitCounts = programPointVisitCounts;
        }

        internal ProgramState SetSymbolicValue(ISymbol symbol, SymbolicValue newSymbolicValue)
        {
            return new ProgramState(
                Values.SetItem(symbol, newSymbolicValue),
                Constraints,
                ProgramPointVisitCounts);
        }

        public SymbolicValue GetSymbolValue(ISymbol symbol)
        {
            if (symbol != null &&
                Values.ContainsKey(symbol))
            {
                return Values[symbol];
            }
            return null;
        }

        internal ProgramState AddVisit(ProgramPoint visitedProgramPoint)
        {
            var visitCount = GetVisitedCount(visitedProgramPoint);
            return new ProgramState(Values, Constraints, ProgramPointVisitCounts.SetItem(visitedProgramPoint, visitCount + 1));
        }

        internal int GetVisitedCount(ProgramPoint programPoint)
        {
            int value;
            if (!ProgramPointVisitCounts.TryGetValue(programPoint, out value))
            {
                value = 0;
            }

            return value;
        }

        internal ProgramState Clean(IEnumerable<ISymbol> liveSymbolsToKeep)
        {
            var cleanedValues = Values
                .Where(sv => liveSymbolsToKeep.Contains(sv.Key))
                .ToImmutableDictionary();

            var usedSymbolicValues = cleanedValues.Values.ToImmutableHashSet();

            var cleanedConstraints = Constraints
                .Where(kv => usedSymbolicValues.Contains(kv.Key) || ProtectedSymbolicValues.Contains(kv.Key))
                .ToImmutableDictionary();

            return new ProgramState(cleanedValues, cleanedConstraints, ProgramPointVisitCounts);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            ProgramState p = obj as ProgramState;
            return Equals(p);
        }

        public bool Equals(ProgramState other)
        {
            if (other == null)
            {
                return false;
            }

            return DictionaryEquals(Values, other.Values) &&
                DictionaryEquals(Constraints, other.Constraints);
        }

        private static bool DictionaryEquals<TKey, TValue>(IDictionary<TKey, TValue> dict1, IDictionary<TKey, TValue> dict2)
        {
            if (dict1 == dict2)
            {
                return true;
            }

            if (dict1 == null ||
                dict2 == null ||
                dict1.Count != dict2.Count)
            {
                return false;
            }

            var valueComparer = EqualityComparer<TValue>.Default;

            foreach (var kvp in dict1)
            {
                TValue value2;
                if (!dict2.TryGetValue(kvp.Key, out value2) ||
                    !valueComparer.Equals(kvp.Value, value2))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hash = 19;

            foreach (var symbolValueAssociation in Values)
            {
                hash = hash * 31 + symbolValueAssociation.Key.GetHashCode();
                hash = hash * 31 + symbolValueAssociation.Value.GetHashCode();
            }

            foreach (var constraint in Constraints)
            {
                hash = hash * 31 + constraint.Key.GetHashCode();
                hash = hash * 31 + constraint.Value.GetHashCode();
            }

            return hash;
        }
    }
}
