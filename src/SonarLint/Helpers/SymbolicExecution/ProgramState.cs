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
        private readonly ImmutableDictionary<ISymbol, SymbolicValue> symbolValueAssociations;
        private readonly ImmutableDictionary<ProgramPoint, int> programPointVisitCounts;

        internal ProgramState()
            : this(ImmutableDictionary<ISymbol, SymbolicValue>.Empty, ImmutableDictionary<ProgramPoint, int>.Empty)
        {
        }

        private ProgramState(ImmutableDictionary<ISymbol, SymbolicValue> symbolValueAssociations,
            ImmutableDictionary<ProgramPoint, int> programPointVisitCounts)
        {
            this.symbolValueAssociations = symbolValueAssociations;
            this.programPointVisitCounts = programPointVisitCounts;
        }

        internal ProgramState SetSymbolicValue(ISymbol symbol, SymbolicValue newSymbolicValue)
        {
            return new ProgramState(
                symbolValueAssociations.SetItem(symbol, newSymbolicValue),
                programPointVisitCounts);
        }

        internal ProgramState AddVisit(ProgramPoint visitedProgramPoint)
        {
            var visitCount = GetVisitedCount(visitedProgramPoint);
            return new ProgramState(symbolValueAssociations, programPointVisitCounts.SetItem(visitedProgramPoint, visitCount + 1));
        }

        internal int GetVisitedCount(ProgramPoint programPoint)
        {
            int value;
            if (!programPointVisitCounts.TryGetValue(programPoint, out value))
            {
                value = 0;
            }

            return value;
        }

        public bool TrySetSymbolicValue(ISymbol symbol, SymbolicValue newSymbolicValue, out ProgramState newProgramState)
        {
            newProgramState = SetSymbolicValue(symbol, newSymbolicValue);

            SymbolicValue oldSymbolicValue;
            if (!symbolValueAssociations.TryGetValue(symbol, out oldSymbolicValue))
            {
                return true;
            }

            if ((oldSymbolicValue == SymbolicValue.True && newSymbolicValue == SymbolicValue.False) ||
                (newSymbolicValue == SymbolicValue.True && oldSymbolicValue == SymbolicValue.False))
            {
                // Contradicting SymbolicValues
                return false;
            }

            if ((oldSymbolicValue.IsDefinitlyNotNull && newSymbolicValue == SymbolicValue.Null) ||
                (newSymbolicValue.IsDefinitlyNotNull && oldSymbolicValue == SymbolicValue.Null))
            {
                // Contradicting SymbolicValues
                return false;
            }

            return true;
        }

        public SymbolicValue GetSymbolValue(ISymbol symbol)
        {
            if (symbol != null &&
                symbolValueAssociations.ContainsKey(symbol))
            {
                return symbolValueAssociations[symbol];
            }
            return null;
        }

        internal ProgramState CleanAndKeepOnly(IEnumerable<ISymbol> liveSymbolsToKeep)
        {
            var deadSymbols = symbolValueAssociations
                .Where(sv => !liveSymbolsToKeep.Contains(sv.Key))
                .Select(sv => sv.Key)
                .ToList();

            return new ProgramState(symbolValueAssociations.RemoveRange(deadSymbols), programPointVisitCounts);
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

            return DictionaryEquals(symbolValueAssociations, other.symbolValueAssociations);
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

            foreach (var symbolValueAssociation in symbolValueAssociations)
            {
                hash = hash * 31 + symbolValueAssociation.Key.GetHashCode();
                hash = hash * 31 + symbolValueAssociation.Value.GetHashCode();
            }

            return hash;
        }
    }
}
