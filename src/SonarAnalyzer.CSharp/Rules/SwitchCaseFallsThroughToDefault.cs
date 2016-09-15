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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("1min")]
    [Rule(DiagnosticId, RuleSeverity, Title, false /* finding rule disabled in SQ */)]
    [Tags(Tag.Clumsy, Tag.Finding)]
    public class SwitchCaseFallsThroughToDefault : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3458";
        internal const string Title = "Empty \"case\" clauses that fall through to the \"default\" should be omitted";
        internal const string Description =
            "Empty \"case\" clauses that fall through to the \"default\" are useless. Whether or not such a \"case\" is " +
            "present, the \"default\" clause will be invoked. Such \"case\"s simply clutter the code, and should be removed.";
        internal const string MessageFormat = "Remove this empty \"case\" clause.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Minor;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), true,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var section = (SwitchSectionSyntax)c.Node;

                    if (section.Statements.Count == 1 ||
                        !section.Labels.Any(label => label.IsKind(SyntaxKind.DefaultSwitchLabel)))
                    {
                        return;
                    }

                    foreach (var label in section.Labels.Where(label => !label.IsKind(SyntaxKind.DefaultSwitchLabel)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, label.GetLocation()));
                    }
                },
                SyntaxKind.SwitchSection);
        }
    }
}
