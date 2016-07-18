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
using SonarLint.Common;
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
        internal ImmutableStack<SymbolicValue> ExpressionStack { get; }

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
                  ImmutableDictionary<ProgramPoint, int>.Empty,
                  ImmutableStack<SymbolicValue>.Empty)
        {
        }

        internal ProgramState(ImmutableDictionary<ISymbol, SymbolicValue> values,
            ImmutableDictionary<SymbolicValue, SymbolicValueConstraint> constraints,
            ImmutableDictionary<ProgramPoint, int> programPointVisitCounts,
            ImmutableStack<SymbolicValue> expressionStack)
        {
            Values = values;
            Constraints = constraints;
            ProgramPointVisitCounts = programPointVisitCounts;
            ExpressionStack = expressionStack;
        }

        public ProgramState PushValue(SymbolicValue symbolicValue)
        {
            return new ProgramState(
                Values,
                Constraints,
                ProgramPointVisitCounts,
                ExpressionStack.Push(symbolicValue));
        }

        public ProgramState PushValues(IEnumerable<SymbolicValue> values)
        {
            if (!values.Any())
            {
                return this;
            }

            return new ProgramState(
                Values,
                Constraints,
                ProgramPointVisitCounts,
                ImmutableStack.Create(ExpressionStack.Concat(values).ToArray()));
        }

        public ProgramState PopValue()
        {
            SymbolicValue poppedValue;
            return PopValue(out poppedValue);
        }

        public ProgramState PopValue(out SymbolicValue poppedValue)
        {
            return new ProgramState(
                Values,
                Constraints,
                ProgramPointVisitCounts,
                ExpressionStack.Pop(out poppedValue));
        }

        public ProgramState PopValues(int numberOfValuesToPop)
        {
            if (numberOfValuesToPop <= 0)
            {
                return this;
            }

            var newStack = ImmutableStack.Create(
                ExpressionStack.Skip(numberOfValuesToPop).ToArray());

            return new ProgramState(
                Values,
                Constraints,
                ProgramPointVisitCounts,
                newStack);
        }

        public SymbolicValue PeekValue()
        {
            return ExpressionStack.Peek();
        }

        internal bool HasValue => !ExpressionStack.IsEmpty;

        internal ProgramState SetSymbolicValue(ISymbol symbol, SymbolicValue newSymbolicValue)
        {
            return new ProgramState(
                Values.SetItem(symbol, newSymbolicValue),
                Constraints,
                ProgramPointVisitCounts,
                ExpressionStack);
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
            return new ProgramState(
                Values,
                Constraints,
                ProgramPointVisitCounts.SetItem(visitedProgramPoint, visitCount + 1),
                ExpressionStack);
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
                .Where(kv =>
                    usedSymbolicValues.Contains(kv.Key) ||
                    ProtectedSymbolicValues.Contains(kv.Key) ||
                    ExpressionStack.Contains(kv.Key))
                .ToImmutableDictionary();

            return new ProgramState(cleanedValues, cleanedConstraints, ProgramPointVisitCounts, ExpressionStack);
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

            return DictionaryHelper.DictionaryEquals(Values, other.Values) &&
                DictionaryHelper.DictionaryEquals(Constraints, other.Constraints) &&
                Enumerable.SequenceEqual(ExpressionStack, other.ExpressionStack);
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

            foreach (var value in ExpressionStack)
            {
                hash = hash * 31 + value.GetHashCode();
            }

            return hash;
        }
    }
}
