/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2017 SonarSource SA
 * mailto: contact AT sonarsource DOT com
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
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [Rule(DiagnosticId)]
    public class PublicMutableFieldsShoudNotBeReadonly : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3887";
        internal const string MessageFormat = "Use an immutable collection here instead or reduce the accessibility of this field";

        private static readonly ISet<KnownType> InvalidMutableTypes = new HashSet<KnownType>
        {
            KnownType.System_Array,
            KnownType.System_Collections_Generic_ICollection_T
        };

        private static readonly ISet<KnownType> AuthorizedTypes = new HashSet<KnownType>
        {
            KnownType.System_Collections_ObjectModel_ReadOnlyCollection_T,
            KnownType.System_Collections_ObjectModel_ReadOnlyDictionary_TKey_TValue
        };

        private static readonly DiagnosticDescriptor rule =
            DiagnosticDescriptorBuilder.GetDescriptor(DiagnosticId, MessageFormat, RspecStrings.ResourceManager);

        protected sealed override DiagnosticDescriptor Rule => rule;

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(CheckForIssue, SyntaxKind.FieldDeclaration);
        }

        private void CheckForIssue(SyntaxNodeAnalysisContext analysisContext)
        {
            var fieldDeclaration = (FieldDeclarationSyntax)analysisContext.Node;
            if (!IsPublicOrProtectedAndReadonly(fieldDeclaration))
            {
                return;
            }

            var symbolInfo = analysisContext.SemanticModel.GetSymbolInfo(fieldDeclaration.Declaration.Type);
            if (IsInvalidMutableType(symbolInfo.Symbol))
            {
                analysisContext.ReportDiagnostic(Diagnostic.Create(Rule, fieldDeclaration.GetLocation()));
            }
        }

        private bool IsPublicOrProtectedAndReadonly(FieldDeclarationSyntax fieldDeclaration)
        {
            const int expectedModifiersCount = 2;

            var modifiersCount =
                fieldDeclaration.Modifiers.Count(m => m.IsKind(SyntaxKind.ReadOnlyKeyword) ||
                                                      m.IsKind(SyntaxKind.PublicKeyword) ||
                                                      m.IsKind(SyntaxKind.ProtectedKeyword));

            return modifiersCount == expectedModifiersCount;
        }

        private bool IsInvalidMutableType(ISymbol symbol)
        {
            var namedTypeSymbol = symbol as INamedTypeSymbol;
            if (namedTypeSymbol != null)
            {
                return IsOrDerivesOrImplementsAny(namedTypeSymbol.ConstructedFrom, InvalidMutableTypes) &&
                    !IsOrDerivesOrImplementsAny(namedTypeSymbol.ConstructedFrom, AuthorizedTypes);
            }

            var typeSymbol = symbol as ITypeSymbol;
            if (typeSymbol != null)
            {
                return IsOrDerivesOrImplementsAny(typeSymbol, InvalidMutableTypes);
            }

            return false;
        }

        private bool IsOrDerivesOrImplementsAny(ITypeSymbol typeSymbol, ISet<KnownType> knownTypes)
        {
            return typeSymbol.IsAny(knownTypes) || typeSymbol.DerivesOrImplementsAny(knownTypes);
        }
    }
}
