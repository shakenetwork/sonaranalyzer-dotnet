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

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;

namespace SonarAnalyzer.Helpers.FlowAnalysis.Common
{
    internal class InvocationVisitor
    {
        private const string EqualsLiteral = "Equals";
        private const string ReferenceEqualsLiteral = "ReferenceEquals";

        private readonly InvocationExpressionSyntax invocation;
        private readonly SemanticModel semanticModel;

        public InvocationVisitor(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            this.invocation = invocation;
            this.semanticModel = semanticModel;
        }

        internal ProgramState GetChangedProgramState(ProgramState programState)
        {
            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            if (IsInstanceEqualsCall(methodSymbol))
            {
                return HandleInstanceEqualsCall(programState);
            }

            if (IsStaticEqualsCall(methodSymbol))
            {
                return HandleStaticEqualsCall(programState);
            }

            if (IsReferenceEqualsCall(methodSymbol))
            {
                return HandleReferenceEqualsCall(invocation, programState);
            }

            if (IsStringNullCheckMethod(methodSymbol))
            {
                return HandleStringNullCheckMethod(programState);
            }

            return programState
                .PopValues((invocation.ArgumentList?.Arguments.Count ?? 0) + 1)
                .PushValue(new SymbolicValue());
        }

        private static ProgramState HandleStringNullCheckMethod(ProgramState programState)
        {
            SymbolicValue arg1;

            var newProgramState = programState
                .PopValue(out arg1)
                .PopValue();

            if (arg1.HasConstraint(ObjectConstraint.Null, newProgramState))
            {
                // Value is null, so the result of the call is true
                return newProgramState.PushValue(SymbolicValue.True);
            }

            return newProgramState.PushValue(new SymbolicValue());
        }

        private static ProgramState HandleStaticEqualsCall(ProgramState programState)
        {
            SymbolicValue arg1;
            SymbolicValue arg2;

            return programState
                .PopValue(out arg1)
                .PopValue(out arg2)
                .PopValue()
                .PushValue(new ValueEqualsSymbolicValue(arg1, arg2));
        }

        private ProgramState HandleReferenceEqualsCall(InvocationExpressionSyntax invocation, ProgramState programState)
        {
            SymbolicValue arg1;
            SymbolicValue arg2;

            var newProgramState = programState
                .PopValue(out arg1)
                .PopValue(out arg2)
                .PopValue();

            var refEquals = new ReferenceEqualsSymbolicValue(arg1, arg2);
            newProgramState = newProgramState.PushValue(refEquals);
            return SetConstraintOnReferenceEquals(refEquals, invocation, arg1, arg2, newProgramState);
        }

        private static ProgramState HandleInstanceEqualsCall(ProgramState programState)
        {
            SymbolicValue arg1;
            SymbolicValue expression;

            var newProgramState = programState
                .PopValue(out arg1)
                .PopValue(out expression);

            var memberAccess = expression as MemberAccessSymbolicValue;

            SymbolicValue arg2 = memberAccess != null
                ? memberAccess.MemberExpression
                : SymbolicValue.This;

            return newProgramState.PushValue(new ValueEqualsSymbolicValue(arg1, arg2));
        }

        private ProgramState SetConstraintOnReferenceEquals(ReferenceEqualsSymbolicValue refEquals, InvocationExpressionSyntax invocation,
            SymbolicValue arg1, SymbolicValue arg2, ProgramState programState)
        {
            if (AreBothArgumentsNull(arg1, arg2, programState))
            {
                return refEquals.SetConstraint(BoolConstraint.True, programState);
            }

            if (IsAnyArgumentNonNullValueType(invocation, arg1, arg2, programState) ||
                ArgumentsHaveDifferentNullability(arg1, arg2, programState))
            {
                return refEquals.SetConstraint(BoolConstraint.False, programState);
            }

            if (arg1 == arg2)
            {
                return refEquals.SetConstraint(BoolConstraint.True, programState);
            }

            return programState;
        }

        private static bool ArgumentsHaveDifferentNullability(SymbolicValue arg1, SymbolicValue arg2, ProgramState programState)
        {
            return arg1.HasConstraint(ObjectConstraint.Null, programState) && arg2.HasConstraint(ObjectConstraint.NotNull, programState) ||
                arg1.HasConstraint(ObjectConstraint.NotNull, programState) && arg2.HasConstraint(ObjectConstraint.Null, programState);
        }

        private bool IsAnyArgumentNonNullValueType(InvocationExpressionSyntax invocation,
            SymbolicValue arg1, SymbolicValue arg2, ProgramState programState)
        {
            var type1 = semanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type;
            var type2 = semanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[1].Expression).Type;

            if (type1 == null ||
                type2 == null)
            {
                return false;
            }

            return IsValueNotNull(arg1, type1, programState) ||
                IsValueNotNull(arg2, type2, programState);
        }

        private static bool IsValueNotNull(SymbolicValue arg, ITypeSymbol type, ProgramState programState)
        {
            return arg.HasConstraint(ObjectConstraint.NotNull, programState) &&
                type.IsValueType;
        }

        private static bool AreBothArgumentsNull(SymbolicValue arg1, SymbolicValue arg2, ProgramState programState)
        {
            return arg1.HasConstraint(ObjectConstraint.Null, programState) &&
                arg2.HasConstraint(ObjectConstraint.Null, programState);
        }

        private static readonly ImmutableHashSet<string> IsNullMethodNames = ImmutableHashSet.Create(
            nameof(string.IsNullOrEmpty),
            nameof(string.IsNullOrWhiteSpace));

        private static bool IsStringNullCheckMethod(IMethodSymbol methodSymbol)
        {
            return methodSymbol != null &&
                methodSymbol.ContainingType.Is(KnownType.System_String) &&
                methodSymbol.IsStatic &&
                IsNullMethodNames.Contains(methodSymbol.Name);
        }

        private static bool IsReferenceEqualsCall(IMethodSymbol methodSymbol)
        {
            return methodSymbol != null &&
                methodSymbol.ContainingType.Is(KnownType.System_Object) &&
                methodSymbol.Name == ReferenceEqualsLiteral;
        }

        private static bool IsInstanceEqualsCall(IMethodSymbol methodSymbol)
        {
            return methodSymbol != null &&
                methodSymbol.Name == EqualsLiteral &&
                !methodSymbol.IsStatic &&
                methodSymbol.Parameters.Length == 1;
        }

        private static bool IsStaticEqualsCall(IMethodSymbol methodSymbol)
        {
            return methodSymbol != null &&
                methodSymbol.ContainingType.Is(KnownType.System_Object) &&
                methodSymbol.IsStatic &&
                methodSymbol.Name == EqualsLiteral;
        }
    }
}
