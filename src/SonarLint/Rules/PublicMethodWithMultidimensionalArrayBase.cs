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

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;

namespace SonarLint.Rules.Common
{
    public abstract class PublicMethodWithMultidimensionalArrayBase : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2368";
        internal const string Title = "Public methods should not have multidimensional array parameters";
        internal const string Description =
            "Exposing methods with multidimensional array parameters require developers to have advanced knowledge about the language in " +
            "order to be able to use them. Moreover, what exactly to pass to such parameters is not intuitive. Therefore, such methods " +
            "should not be exposed, but can be used internally.";
        internal const string MessageFormat = "Make this method private or simplify its parameters to not use multidimensional arrays.";
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        internal static bool IsPublic(ISymbol symbol)
        {
            var currentSymbol = symbol;
            while (currentSymbol != null &&
                currentSymbol.DeclaredAccessibility == Accessibility.Public)
            {
                currentSymbol = currentSymbol.ContainingSymbol;
            }

            return currentSymbol == null || currentSymbol.DeclaredAccessibility == Accessibility.NotApplicable;
        }
    }

    public abstract class PublicMethodWithMultidimensionalArrayBase<TLanguageKindEnum, TMethodSyntax> : PublicMethodWithMultidimensionalArrayBase
        where TLanguageKindEnum : struct
        where TMethodSyntax: SyntaxNode
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var method = (TMethodSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

                    if (methodSymbol != null &&
                        IsMethodChangeable(methodSymbol) &&
                        IsPublic(methodSymbol) &&
                        MethodHasMultidimensionalArrayParameters(methodSymbol))
                    {
                        var identifier = GetIdentifier(method);
                        c.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation()));
                    }
                },
                SyntaxKindsOfInterest.ToArray());
        }

        private static bool MethodHasMultidimensionalArrayParameters(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters
                .Select(param => param.Type as IArrayTypeSymbol)
                .Where(type => type != null)
                .Any(type => type.Rank > 1 || type.ElementType is IArrayTypeSymbol);
        }

        protected abstract SyntaxToken GetIdentifier(TMethodSyntax method);

        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }

        private static bool IsMethodChangeable(IMethodSymbol methodSymbol)
        {
            if (methodSymbol.IsOverride)
            {
                return false;
            }

            return !methodSymbol.ContainingType
                .AllInterfaces
                .SelectMany(@interface => @interface.GetMembers().OfType<IMethodSymbol>())
                .Any(method => methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
        }
    }
}
