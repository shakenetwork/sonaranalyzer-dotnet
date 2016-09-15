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
using System.Linq;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules
{
    public abstract class EnumNameHasEnumSuffixBase : SonarDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S2344";
        protected const string Title = "Enumeration type names should not have \"Flags\" or \"Enum\" suffixes";
        protected const string Description =
            "The information that an enumeration type is actually an enumeration or a set of flags should not be duplicated in its name.";
        protected const string MessageFormat = "Rename this enumeration to remove the \"{0}\" suffix.";
        protected const string Category = SonarAnalyzer.Common.Category.Naming;
        protected const Severity RuleSeverity = Severity.Minor;
        protected const bool IsActivatedByDefault = true;

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        protected static readonly IEnumerable<string> NameEndings = ImmutableArray.Create("enum", "flags");

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }
    }

    public abstract class EnumNameHasEnumSuffixBase<TLanguageKindEnum> : EnumNameHasEnumSuffixBase
        where TLanguageKindEnum : struct
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var identifier = GetIdentifier(c.Node);
                    var name = identifier.ValueText;

                    var nameEnding = NameEndings.FirstOrDefault(ending => name.EndsWith(ending, System.StringComparison.OrdinalIgnoreCase));
                    if (nameEnding != null)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(),
                            name.Substring(name.Length - nameEnding.Length, nameEnding.Length)));
                    }
                },
                SyntaxKindsOfInterest.ToArray());
        }

        protected abstract SyntaxToken GetIdentifier(SyntaxNode node);
        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
    }
}
