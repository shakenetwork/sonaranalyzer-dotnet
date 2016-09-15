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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Convention)]
    public sealed class PublicFieldName : FieldNameChecker
    {
        internal const string DiagnosticId = "S2369";
        internal const string Title = "Non-private fields should comply with a naming convention";
        internal const string Description =
            "Shared coding conventions allow teams to collaborate efficiently. This rule checks that all non-private fields " +
            "names match a provided regular expression.";
        internal const string MessageFormat = "Rename \"{0}\" to match the regular expression: \"{1}\".";

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);        

        [RuleParameter("format", PropertyType.String,
            "Regular expression used to check the non-private field names against.", PascalCasingPattern)]
        public override string Pattern { get; set; } = PascalCasingPattern;

        protected override bool IsCandidateSymbol(IFieldSymbol symbol)
        {
            return symbol.DeclaredAccessibility != Accessibility.Private &&
                !symbol.IsConst &&
                !(symbol.IsShared() && symbol.IsReadOnly);
        }
    }
}
