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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class VirtualEventField : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2290";
        internal const string Title = "Field-like events should not be virtual";
        internal const string Description =
            "Field-like events are events that do not have explicit \"add\" and \"remove\" methods. The compiler generates " +
            "a \"private\" \"delegate\" field to back the event, as well as generating the implicit \"add\" and \"remove\" " +
            "methods. When a \"virtual\" field-like \"event\" is overridden by another field-like \"event\", the behavior " +
            "of the C# compiler is to generate a new \"private\" \"delegate\" field in the derived class, separate from the " +
            "parent's field. This results in multiple and separate events being created, which is rarely what's actually " +
            "intended.";
        internal const string MessageFormat = "Remove this virtual of {0}.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var eventField = (EventFieldDeclarationSyntax) c.Node;

                    if (eventField.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.VirtualKeyword)))
                    {
                        var virt = eventField.Modifiers.First(modifier => modifier.IsKind(SyntaxKind.VirtualKeyword));
                        var names = string.Join(", ", eventField.Declaration.Variables
                            .Select(syntax => $"\"{syntax.Identifier.ValueText}\"")
                            .OrderBy(s => s));
                        c.ReportDiagnostic(Diagnostic.Create(Rule, virt.GetLocation(), names));
                    }
                },
                SyntaxKind.EventFieldDeclaration);
        }
    }
}
