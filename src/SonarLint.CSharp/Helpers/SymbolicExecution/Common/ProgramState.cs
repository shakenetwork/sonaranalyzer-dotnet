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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SonarLint.Helpers.FlowAnalysis.Common
{
    public class ProgramState
    {
        private readonly Dictionary<ISymbol, SymbolicValue> symbolValueAssociations;
        private readonly Dictionary<SymbolicValue, SymbolicValueConstraint> constraints;

        internal ProgramState()
            : this(new Dictionary<ISymbol, SymbolicValue>(),
                  new Dictionary<SymbolicValue, SymbolicValueConstraint>())
        {
        }

        private ProgramState(Dictionary<ISymbol, SymbolicValue> symbolValueAssociation,
            Dictionary<SymbolicValue, SymbolicValueConstraint> constraints)
        {
            this.symbolValueAssociations = symbolValueAssociation;
            this.constraints = constraints;
        }

        internal ProgramState AddSymbolValue(ISymbol symbol, SymbolicValue value)
        {
            var ret = new ProgramState(
                new Dictionary<ISymbol, SymbolicValue>(symbolValueAssociations),
                new Dictionary<SymbolicValue, SymbolicValueConstraint>(constraints));
            ret.symbolValueAssociations[symbol] = value;
            return ret;
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

        internal ProgramState AddSymbolicValueConstraint(SymbolicValue symbolicValue, SymbolicValueConstraint constraint)
        {
            var newProgramState = new ProgramState(
                new Dictionary<ISymbol, SymbolicValue>(symbolValueAssociations),
                new Dictionary<SymbolicValue, SymbolicValueConstraint>(constraints));
            newProgramState.constraints[symbolicValue] = constraint;
            return newProgramState;
        }

        public bool TryGetBoolValue(ISymbol symbol, out bool value)
        {
            value = false;
            if (!symbolValueAssociations.ContainsKey(symbol))
            {
                return false;
            }

            var symbolicValue = symbolValueAssociations[symbol];
            if (!constraints.ContainsKey(symbolicValue))
            {
                return false;
            }

            var constraint = constraints[symbolicValue];
            value = constraint == BooleanLiteralConstraint.True;
            return true;
        }

        internal ProgramState CleanAndKeepOnly(IEnumerable<ISymbol> liveSymbolsToKeep)
        {
            var ret = new ProgramState(
                new Dictionary<ISymbol, SymbolicValue>(symbolValueAssociations),
                new Dictionary<SymbolicValue, SymbolicValueConstraint>(constraints));

            var deadSymbols = ret.symbolValueAssociations
                .Where(sv => !liveSymbolsToKeep.Contains(sv.Key))
                .ToList();

            foreach (var deadSymbol in deadSymbols)
            {
                ret.symbolValueAssociations.Remove(deadSymbol.Key);
            }

            var symbolicValuesLeft = new HashSet<SymbolicValue>(ret.symbolValueAssociations.Values);

            foreach (var value in ret.constraints.Keys.ToList())
            {
                if (!symbolicValuesLeft.Contains(value))
                {
                    ret.constraints.Remove(value);
                }
            }

            return ret;
        }
    }
}
