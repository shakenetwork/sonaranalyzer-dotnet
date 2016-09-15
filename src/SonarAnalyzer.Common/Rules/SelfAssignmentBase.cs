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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [SqaleConstantRemediation("3min")]
    [Tags(Tag.Bug, Tag.Cert)]
    public abstract class SelfAssignmentBase : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1656";
        internal const string Title = "Variables should not be self-assigned";
        internal const string Description =
            "There is no reason to re-assign a variable to itself. Either this statement is redundant and should " +
            "be removed, or the re-assignment is a mistake and some other value or variable was intended for the " +
            "assignment instead.";
        internal const string MessageFormat = "Remove or correct this useless self-assignment.";
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