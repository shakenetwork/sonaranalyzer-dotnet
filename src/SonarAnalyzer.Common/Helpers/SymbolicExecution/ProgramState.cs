/*
 * SonarAnalyzer for .NET
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
using SonarAnalyzer.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SonarAnalyzer.Helpers.FlowAnalysis.Common
{
    public class ProgramState : IEquatable<ProgramState>
    {
        internal ImmutableDictionary<ISymbol, SymbolicValue> Values { get; }
        internal ImmutableDictionary<SymbolicValue, SymbolicValueConstraint> Constraints { get; }
        internal ImmutableDictionary<ProgramPoint, int> ProgramPointVisitCounts { get; }
        internal ImmutableStack<SymbolicValue> ExpressionStack { get; }
        internal ImmutableHashSet<BinaryRelationship> Relationships { get; }

        private static ImmutableDictionary<SymbolicValue, SymbolicValueConstraint> InitialConstraints =>
            new Dictionary<SymbolicValue, SymbolicValueConstraint>
            {
                { SymbolicValue.True, BoolConstraint.True },
                { SymbolicValue.False, BoolConstraint.False },
                { SymbolicValue.Null, ObjectConstraint.Null },
                { SymbolicValue.This, ObjectConstraint.NotNull },
                { SymbolicValue.Base, ObjectConstraint.NotNull }
            }.ToImmutableDictionary();

        private static readonly ISet<SymbolicValue> ProtectedSymbolicValues = ImmutableHashSet.Create(
            SymbolicValue.True,
            SymbolicValue.False,
            SymbolicValue.Null,
            SymbolicValue.This,
            SymbolicValue.Base);

        private static readonly ISet<SymbolicValue> DistinguishedReferences = ImmutableHashSet.Create(
            SymbolicValue.This,
            SymbolicValue.Base);

        internal ProgramState()
            : this(ImmutableDictionary<ISymbol, SymbolicValue>.Empty,
                  InitialConstraints,
                  ImmutableDictionary<ProgramPoint, int>.Empty,
                  ImmutableStack<SymbolicValue>.Empty,
                  ImmutableHashSet<BinaryRelationship>.Empty)
        {
        }

        internal ProgramState TrySetRelationship(BinaryRelationship relationship)
        {
            if (Relationships.Contains(relationship))
            {
                return this;
            }

            var leftOp = relationship.LeftOperand;
            var rightOp = relationship.RightOperand;

            // Only add relationships on SV's that belong to a local symbol
            var localValues = Values.Values
                .Except(ProtectedSymbolicValues)
                .Concat(DistinguishedReferences)
                .ToImmutableHashSet();

            if (!localValues.Contains(leftOp) ||
                !localValues.Contains(rightOp))
            {
                return this;
            }

            if (relationship.IsContradicting(Relationships))
            {
                return null;
            }

            return new ProgramState(
                Values,
                Constraints,
                ProgramPointVisitCounts,
                ExpressionStack,
                Relationships.Add(relationship));
        }

        internal ProgramState(ImmutableDictionary<ISymbol, SymbolicValue> values,
            ImmutableDictionary<SymbolicValue, SymbolicValueConstraint> constraints,
            ImmutableDictionary<ProgramPoint, int> programPointVisitCounts,
            ImmutableStack<SymbolicValue> expressionStack,
            ImmutableHashSet<BinaryRelationship> relationships)
        {
            Values = values;
            Constraints = constraints;
            ProgramPointVisitCounts = programPointVisitCounts;
            ExpressionStack = expressionStack;
            Relationships = relationships;
        }

        public ProgramState PushValue(SymbolicValue symbolicValue)
        {
            return new ProgramState(
                Values,
                Constraints,
                ProgramPointVisitCounts,
                ExpressionStack.Push(symbolicValue),
                Relationships);
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
                ImmutableStack.Create(ExpressionStack.Concat(values).ToArray()),
                Relationships);
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
                ExpressionStack.Pop(out poppedValue),
                Relationships);
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
                newStack,
                Relationships);
        }

        public SymbolicValue PeekValue()
        {
            return ExpressionStack.Peek();
        }

        public SymbolicValue PeekValue(int nth)
        {
            return ExpressionStack.ToList()[nth];
        }

        internal bool HasValue => !ExpressionStack.IsEmpty;

        internal ProgramState SetSymbolicValue(ISymbol symbol, SymbolicValue newSymbolicValue)
        {
            return new ProgramState(
                Values.SetItem(symbol, newSymbolicValue),
                Constraints,
                ProgramPointVisitCounts,
                ExpressionStack,
                Relationships);
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
                ExpressionStack,
                Relationships);
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

            // SVs for live symbols
            var usedSymbolicValues = cleanedValues.Values
                .Concat(DistinguishedReferences)
                .ToImmutableHashSet();

            var cleanedConstraints = Constraints
                .Where(kv =>
                    usedSymbolicValues.Contains(kv.Key) ||
                    ProtectedSymbolicValues.Contains(kv.Key) ||
                    ExpressionStack.Contains(kv.Key))
                .ToImmutableDictionary();

            // Relationships for live symbols (no transitivity, so both of them need to be live in order to hold any information)
            var cleanedRelationships = Relationships
                .Where(r =>
                    usedSymbolicValues.Contains(r.LeftOperand) ||
                    usedSymbolicValues.Contains(r.RightOperand))
                .ToImmutableHashSet();

            return new ProgramState(cleanedValues, cleanedConstraints, ProgramPointVisitCounts, ExpressionStack, cleanedRelationships);
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
                Enumerable.SequenceEqual(ExpressionStack, other.ExpressionStack) &&
                Relationships.SetEquals(other.Relationships);
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

            foreach (var relationship in Relationships)
            {
                hash = hash * 31 + relationship.GetHashCode();
            }

            return hash;
        }
    }
}
