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
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarAnalyzer.Helpers
{
    internal static class SymbolHelper
    {
        public static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(this INamespaceSymbol @namespace)
        {
            if (@namespace == null)
            {
                yield break;
            }

            foreach (var typeMember in @namespace.GetTypeMembers().SelectMany(t => GetAllNamedTypes(t)))
            {
                yield return typeMember;
            }

            foreach (var typeMember in @namespace.GetNamespaceMembers().SelectMany(t => GetAllNamedTypes(t)))
            {
                yield return typeMember;
            }
        }

        public static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(this INamedTypeSymbol type)
        {
            if (type == null)
            {
                yield break;
            }

            yield return type;

            foreach (var nestedType in type.GetTypeMembers().SelectMany(t => GetAllNamedTypes(t)))
            {
                yield return nestedType;
            }
        }

        public static bool IsInterfaceImplementationOrMemberOverride(this ISymbol symbol)
        {
            ISymbol overriddenSymbol;
            return TryGetOverriddenOrInterfaceMember(symbol, out overriddenSymbol);
        }

        public static bool TryGetOverriddenOrInterfaceMember<T>(this T symbol, out T overriddenSymbol)
            where T : class, ISymbol
        {
            if (symbol == null ||
                !CanSymbolBeInterfaceMemberOrOverride(symbol))
            {
                overriddenSymbol = null;
                return false;
            }

            if (symbol.IsOverride)
            {
                overriddenSymbol = GetOverriddenMember(symbol);
                return overriddenSymbol != null;
            }

            overriddenSymbol = symbol.ContainingType
                .AllInterfaces
                .SelectMany(@interface => @interface.GetMembers())
                .OfType<T>()
                .FirstOrDefault(member => symbol.Equals(symbol.ContainingType.FindImplementationForInterfaceMember(member)));
            return overriddenSymbol != null;
        }

        private static T GetOverriddenMember<T>(this T symbol)
            where T : class, ISymbol
        {
            switch (symbol.Kind)
            {
                case SymbolKind.Method:
                    return (T)((IMethodSymbol)symbol).OverriddenMethod;
                case SymbolKind.Property:
                    return (T)((IPropertySymbol)symbol).OverriddenProperty;
                case SymbolKind.Event:
                    return (T)((IEventSymbol)symbol).OverriddenEvent;
                default:
                    throw new ArgumentException(
                        $"Only methods, properties and events can be overriden. {typeof(T).Name} was provided",
                        nameof(symbol));
            }
        }

        private static bool CanSymbolBeInterfaceMemberOrOverride(ISymbol symbol)
        {
            return symbol is IMethodSymbol ||
                symbol is IPropertySymbol ||
                symbol is IEventSymbol;
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

        public static bool IsChangeable(this ISymbol symbol)
        {
            return !symbol.IsAbstract &&
                !symbol.IsVirtual &&
                !symbol.IsInterfaceImplementationOrMemberOverride();
        }

        public static bool IsProbablyEventHandler(this IMethodSymbol methodSymbol)
        {
            if (!methodSymbol.ReturnsVoid ||
                methodSymbol.Parameters.Length != 2)
            {
                return false;
            }

            var eventArgs = methodSymbol.Parameters[1];
            var eventArgsType = eventArgs.Type as INamedTypeSymbol;

            return eventArgsType == null ||
                eventArgsType.DerivesOrImplements(KnownType.System_EventArgs);
        }

        public static bool IsExtensionOn(this IMethodSymbol methodSymbol, KnownType type)
        {
            if (methodSymbol == null ||
                !methodSymbol.IsExtensionMethod)
            {
                return false;
            }

            var receiverType = methodSymbol.ReceiverType as INamedTypeSymbol;

            if (methodSymbol.MethodKind == MethodKind.Ordinary)
            {
                receiverType = methodSymbol.Parameters.First().Type as INamedTypeSymbol;
            }

            var constructedFrom = receiverType?.ConstructedFrom;
            return constructedFrom.Is(type);
        }

        public static IEnumerable<IParameterSymbol> GetParameters(this ISymbol symbol)
        {
            var methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                return methodSymbol.Parameters;
            }

            var propertySymbol = symbol as IPropertySymbol;
            if (propertySymbol != null)
            {
                return propertySymbol.Parameters;
            }

            return Enumerable.Empty<IParameterSymbol>();
        }
    }
}
