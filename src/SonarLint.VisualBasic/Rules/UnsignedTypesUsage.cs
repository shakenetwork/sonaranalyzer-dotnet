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

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class UnsignedTypesUsage : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2374";
        internal const string Title = "Signed types should be preferred to unsigned ones";
        internal const string Description =
            "Unsigned integers have different arithmetic operators than signed ones - operators that few developers understand. " +
            "Therefore, signed types should be preferred where possible.";
        internal const string MessageFormat = "Change this unsigned type to \"{0}\".";
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var typeSyntax = (TypeSyntax)c.Node;
                    if (typeSyntax.Parent is QualifiedNameSyntax)
                    {
                        return;
                    }

                    var typeSymbol = c.SemanticModel.GetTypeInfo(typeSyntax).Type;
                    if (typeSymbol == null)
                    {
                        return;
                    }

                    if (typeSymbol.SpecialType == SpecialType.System_UInt16 ||
                        typeSymbol.SpecialType == SpecialType.System_UInt32 ||
                        typeSymbol.SpecialType == SpecialType.System_UInt64)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, typeSyntax.GetLocation(),
                            SignedPairs[typeSymbol.SpecialType]));
                    }
                },
                SyntaxKind.PredefinedType,
                SyntaxKind.IdentifierName,
                SyntaxKind.QualifiedName);
        }

        private static readonly IDictionary<SpecialType, string> SignedPairs =
            new Dictionary<SpecialType, string>
            {
                {SpecialType.System_UInt16, "Short"},
                {SpecialType.System_UInt32, "Integer"},
                {SpecialType.System_UInt64, "Long"}
            };
    }
}
