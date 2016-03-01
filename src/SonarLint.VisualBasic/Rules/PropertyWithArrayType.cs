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
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.CpuEfficiency)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Performance)]
    public class PropertyWithArrayType : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2365";
        internal const string Title = "Properties should not be based on arrays";
        internal const string Description =
            "Most developers expect property access to be as efficient as field access. However, if a property returns an array, it must return a " +
            "deep copy of the original array or risk having the object's internal state altered unexpectedly. However, making a deep copy, especially " +
            "when the array is large, is much slower than a simple field access.Therefore, such properties should be refactored into methods.";
        internal const string MessageFormat = "Refactor \"{0}\" into a method, properties should not be based on arrays.";
        internal const string Category = SonarLint.Common.Category.Design;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var propertyStatement = (PropertyStatementSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(propertyStatement);
                    if (symbol != null &&
                        symbol.Type is IArrayTypeSymbol)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, propertyStatement.Identifier.GetLocation(), symbol.Name));
                    }
                },
                SyntaxKind.PropertyStatement);
        }
    }
}
