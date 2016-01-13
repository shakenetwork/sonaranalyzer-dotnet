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

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace SonarLint.Helpers
{
    // todo: this should come from the Roslyn API (https://github.com/dotnet/roslyn/issues/9)
    internal class MethodParameterLookup
    {
        private readonly InvocationExpressionSyntax invocation;
        private readonly IMethodSymbol methodSymbol;

        public MethodParameterLookup(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            this.invocation = invocation;
            methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        }

        public IMethodSymbol MethodSymbol
        {
            get
            {
                return methodSymbol;
            }
        }

        public static IParameterSymbol GetParameterSymbol(ArgumentSyntax argument, ArgumentListSyntax argumentList, IMethodSymbol method)
        {
            if (!argumentList.Arguments.Contains(argument) ||
                method == null)
            {
                return null;
            }

            if (argument.NameColon != null)
            {
                return method.Parameters
                    .FirstOrDefault(symbol => symbol.Name == argument.NameColon.Name.Identifier.ValueText);
            }

            var argumentIndex = argumentList.Arguments.IndexOf(argument);
            var parameterIndex = argumentIndex;

            if (parameterIndex >= method.Parameters.Length)
            {
                var p = method.Parameters.Last();
                return p.IsParams ? p : null;
            }
            var parameter = method.Parameters[parameterIndex];
            return parameter;
        }

        public IParameterSymbol GetParameterSymbol(ArgumentSyntax argument)
        {
            return GetParameterSymbol(argument, invocation.ArgumentList, methodSymbol);
        }
    }
}
