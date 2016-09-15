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
    [SqaleConstantRemediation("10min")]
    [Tags(Tag.Bug, Tag.Cert, Tag.Pitfall, Tag.Unused)]
    public abstract class ConditionalStructureSameConditionBase : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1862";
        internal const string Title = "Related \"if/else if\" (\"If\"/\"ElseIf\") statements should not have the same condition";
        internal const string Description =
            "A chain of \"if\"/\"else if\" (\"If\"/\"ElseIf\") statements is evaluated from top to bottom. At most, " +
            "only one branch will be executed: the first one with a condition that evaluates to " +
            "\"true\". Therefore, duplicating a condition automatically leads to dead code. " +
            "Usually, this is due to a copy/paste error. At best, it's simply dead code and at " +
            "worst, it's a bug that is likely to induce further bugs as the code is maintained, " +
            "and obviously it could lead to unexpected behavior.";
        internal const string MessageFormat = "This branch duplicates the one on line {0}.";
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
