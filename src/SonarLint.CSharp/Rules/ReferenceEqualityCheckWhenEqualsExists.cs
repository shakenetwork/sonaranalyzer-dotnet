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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Linq;
using System.Collections.Generic;
using System;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cert, Tag.Cwe)]
    public class ReferenceEqualityCheckWhenEqualsExists : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1698";
        internal const string Title = "\"==\" should not be used when \"Equals\" is overridden";
        internal const string Description =
            "Using the equality \"==\" and inequality \"!=\" operators to compare two objects generally works. The operators can be " +
            "overloaded, and therefore the comparison can resolve to the appropriate method. However, when the operators are used on " +
            "interface instances, then \"==\" resolves to reference equality, which may result in unexpected behavior  if implementing " +
            "classes override \"Equals\". Similarly, when a class overrides \"Equals\", but instances are compared with non-overloaded " +
            "\"==\", there is a high chance that value comparison was meant instead of the reference one.";
        internal const string MessageFormat = "Consider using \"Equals\" if value comparison was intended.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(
                compilationStartContext =>
                {
                    var allNamedTypeSymbols = AllNamedTypeSymbols(compilationStartContext.Compilation.GlobalNamespace);
                    var allInterfacesWithImplementationsOverridenEquals =
                        allNamedTypeSymbols
                            .Where(t => t.AllInterfaces.Any() && HasEqualsOverride(t))
                            .SelectMany(t => t.AllInterfaces)
                            .ToImmutableHashSet();

                    compilationStartContext.RegisterSyntaxNodeActionInNonGenerated(
                        c =>
                        {
                            var binary = (BinaryExpressionSyntax)c.Node;
                            if (!IsBinaryCandidateForReporting(binary, c.SemanticModel))
                            {
                                return;
                            }

                            var typeLeft = c.SemanticModel.GetTypeInfo(binary.Left).Type;
                            var typeRight = c.SemanticModel.GetTypeInfo(binary.Right).Type;
                            if (typeLeft == null || typeRight == null)
                            {
                                return;
                            }

                            if (IsSystemType(typeLeft) ||
                                IsSystemType(typeRight))
                            {
                                return;
                            }

                            if (MightOverrideEquals(typeLeft, allInterfacesWithImplementationsOverridenEquals) ||
                                MightOverrideEquals(typeRight, allInterfacesWithImplementationsOverridenEquals))
                            {
                                c.ReportDiagnostic(Diagnostic.Create(Rule, binary.OperatorToken.GetLocation()));
                            }
                        },
                        SyntaxKind.EqualsExpression,
                        SyntaxKind.NotEqualsExpression);
                });
        }

        private static bool MightOverrideEquals(ITypeSymbol type, ISet<INamedTypeSymbol> allInterfacesWithImplementationsOverridenEquals)
        {
            return HasEqualsOverride(type) ||
                allInterfacesWithImplementationsOverridenEquals.Contains(type) ||
                HasTypeConstraintsWhichMightOverrideEquals(type, allInterfacesWithImplementationsOverridenEquals);
        }

        private static bool HasTypeConstraintsWhichMightOverrideEquals(ITypeSymbol type, ISet<INamedTypeSymbol> allInterfacesWithImplementationsOverridenEquals)
        {
            if (type.TypeKind != TypeKind.TypeParameter)
            {
                return false;
            }

            var typeParameter = (ITypeParameterSymbol)type;
            return typeParameter.ConstraintTypes.Any(t => MightOverrideEquals(t, allInterfacesWithImplementationsOverridenEquals));
        }

        private static IEnumerable<INamedTypeSymbol> AllNamedTypeSymbols(INamespaceSymbol ns)
        {
            foreach (var typeMember in ns.GetTypeMembers())
            {
                yield return typeMember;
            }

            foreach (var nestedNs in ns.GetNamespaceMembers())
            {
                foreach (var nestedTypeMember in AllNamedTypeSymbols(nestedNs))
                {
                    yield return nestedTypeMember;
                }
            }
        }

        private static bool IsSystemType(ITypeSymbol type)
        {
            return type.ToDisplayString() == SystemTypeName;
        }

        private static bool IsBinaryCandidateForReporting(BinaryExpressionSyntax binary, SemanticModel semanticModel)
        {
            var equalitySymbol = semanticModel.GetSymbolInfo(binary).Symbol as IMethodSymbol;

            return IsMethodDefinedOnObject(equalitySymbol) &&
                !IsInEqualsOverride(semanticModel.GetEnclosingSymbol(binary.SpanStart) as IMethodSymbol);
        }

        private const string EqualsName = "Equals";
        private const string SystemTypeName = "System.Type";

        private static bool IsMethodDefinedOnObject(IMethodSymbol equalitySymbol)
        {
            return equalitySymbol != null &&
                equalitySymbol.ContainingType != null &&
                equalitySymbol.ContainingType.SpecialType == SpecialType.System_Object;
        }

        private static bool HasEqualsOverride(ITypeSymbol type)
        {
            return GetEqualsOverrides(type).Any(m => IsMethodDefinedOnObject(m.OverriddenMethod));
        }

        private static IEnumerable<IMethodSymbol> GetEqualsOverrides(ITypeSymbol type)
        {
            if (type == null)
            {
                return Enumerable.Empty<IMethodSymbol>();
            }

            var candidateEqualsMethods = new HashSet<IMethodSymbol>();

            var currentType = type;
            while(currentType != null &&
                currentType.SpecialType != SpecialType.System_Object)
            {
                candidateEqualsMethods.UnionWith(currentType.GetMembers(EqualsName)
                    .OfType<IMethodSymbol>()
                    .Where(method => method.IsOverride && method.OverriddenMethod != null));

                currentType = currentType.BaseType;
            }

            return candidateEqualsMethods;
        }

        private static bool IsInEqualsOverride(IMethodSymbol method)
        {
            if (method == null)
            {
                return false;
            }

            var currentMethod = method;
            while (currentMethod != null)
            {
                if (currentMethod.Name == EqualsName &&
                    IsMethodDefinedOnObject(currentMethod))
                {
                    return true;
                }

                currentMethod = currentMethod.OverriddenMethod;
            }
            return false;
        }
    }
}
