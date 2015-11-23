/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
using System.Linq;

namespace SonarLint.Helpers
{
    public static class SymbolHelper
    {
        public static bool IsInterfaceImplementationOrMemberOverride(this ISymbol symbol)
        {
            if (symbol == null)
            {
                return false;
            }

            if (!(symbol is IMethodSymbol) &&
                !(symbol is IPropertySymbol))
            {
                return false;
            }

            if (symbol.IsOverride)
            {
                return true;
            }

            return symbol.ContainingType
                .AllInterfaces
                .SelectMany(@interface => @interface.GetMembers())
                .Where(member => member is IMethodSymbol || member is IPropertySymbol)
                .Any(member => symbol.Equals(symbol.ContainingType.FindImplementationForInterfaceMember(member)));
        }

        public static bool IsPublicApi(this ISymbol symbol)
        {
            var currentSymbol = symbol;
            while (currentSymbol != null &&
                currentSymbol.DeclaredAccessibility == Accessibility.Public)
            {
                currentSymbol = currentSymbol.ContainingSymbol;
            }

            return currentSymbol == null || currentSymbol.DeclaredAccessibility == Accessibility.NotApplicable;
        }

        public static IEnumerable<INamedTypeSymbol> GetSelfAndBaseTypes(this INamedTypeSymbol type)
        {
            if (type == null)
            {
                yield break;
            }

            var baseType = type;
            while (baseType != null &&
                !(baseType is IErrorTypeSymbol))
            {
                yield return baseType;
                baseType = baseType.BaseType;
            }
        }

        public static bool DerivesOrImplementsAny(this INamedTypeSymbol type, params ITypeSymbol[] possibleTypes)
        {
            var allInterfaces = type.AllInterfaces.Select(inter => inter.ConstructedFrom);
            if (allInterfaces.Intersect(possibleTypes).Any())
            {
                return true;
            }

            return type.GetSelfAndBaseTypes().Any(baseType => possibleTypes.Contains(baseType));
        }

        public static bool IsChangeable(this IMethodSymbol methodSymbol)
        {
            return !methodSymbol.IsAbstract &&
                !methodSymbol.IsVirtual &&
                !methodSymbol.IsInterfaceImplementationOrMemberOverride();
        }

        public static bool IsProbablyEventHandler(this IMethodSymbol methodSymbol, Compilation compilation)
        {
            if (!methodSymbol.ReturnsVoid ||
                methodSymbol.Parameters.Length != 2)
            {
                return false;
            }

            var eventArgs = methodSymbol.Parameters[1];
            var eventArgsType = eventArgs.Type as INamedTypeSymbol;
            if (eventArgsType == null)
            {
                return true;
            }

            var sysEventArgs = compilation.GetTypeByMetadataName("System.EventArgs");
            if (sysEventArgs == null)
            {
                return true;
            }

            return eventArgsType.DerivesOrImplementsAny(sysEventArgs);
        }
    }
}
