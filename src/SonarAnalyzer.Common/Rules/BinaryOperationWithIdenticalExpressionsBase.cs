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
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Common.Sqale;

namespace SonarAnalyzer.Rules
{
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("2min")]
    [Tags(Tag.Bug, Tag.Cert)]
    public abstract class BinaryOperationWithIdenticalExpressionsBase : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1764";
        internal const string Title = "Identical expressions should not be used on both sides of a binary operator";
        internal const string Description =
            "Using the same value on either side of a binary operator is almost always a " +
            "mistake. In the case of logical operators, it is either a copy/paste error " +
            "and therefore a bug, or it is simply wasted code, and should be simplified. " +
            "In the case of bitwise operators and most binary mathematical operators, " +
            "having the same value on both sides of an operator yields predictable results, " +
            "and should be simplified.";
        internal const string MessageFormat = "Identical sub-expressions on both sides of operator \"{0}\".";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);
    }
}