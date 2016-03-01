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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    [Tags(Tag.Clumsy, Tag.Finding)]
    public class RedundantPropertyNamesInAnonymousClass : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3441";
        internal const string Title = "Redundant property names should be omitted in anonymous classes";
        internal const string Description =
            "When an anonymous type's properties are copied from properties or variables with the same names, it yields cleaner " +
            "code to omit the new type's property name and the assignment operator.";
        internal const string MessageFormat = "Remove the redundant \"{0} =\".";
        internal const string Category = SonarLint.Common.Category.Maintainability;
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
                    var anonymousObjectCreation = (AnonymousObjectCreationExpressionSyntax)c.Node;

                    foreach (var initializer in GetRedundantInitializers(anonymousObjectCreation.Initializers))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, initializer.NameEquals.GetLocation(),
                            initializer.NameEquals.Name.Identifier.ValueText));
                    }
                },
                SyntaxKind.AnonymousObjectCreationExpression);
        }

        private static IEnumerable<AnonymousObjectMemberDeclaratorSyntax> GetRedundantInitializers(
            IEnumerable<AnonymousObjectMemberDeclaratorSyntax> initializers)
        {
            var initializersToReportOn = new List<AnonymousObjectMemberDeclaratorSyntax>();

            foreach (var initializer in initializers.Where(initializer => initializer.NameEquals != null))
            {
                var identifier = initializer.Expression as IdentifierNameSyntax;
                if (identifier != null &&
                    identifier.Identifier.ValueText == initializer.NameEquals.Name.Identifier.ValueText)
                {
                    initializersToReportOn.Add(initializer);
                }
            }

            return initializersToReportOn;
        }
    }
}
