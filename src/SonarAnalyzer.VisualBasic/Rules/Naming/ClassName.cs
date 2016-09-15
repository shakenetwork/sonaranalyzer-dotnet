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
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, true)]
    [Tags(Tag.Convention)]
    public class ClassName : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S101";
        internal const string Title = "Class names should comply with a naming convention";
        internal const string Description =
            "Sharing some naming conventions is a key point to make it possible for a team to efficiently collaborate. " +
            "This rule allows to check that all class names match a provided regular expression.";
        internal const string MessageFormat = "Rename this class to match the regular expression: \"{0}\".";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), false,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        [RuleParameter("format", PropertyType.String,
            "Regular expression used to check the class names against.", FieldNameChecker.PascalCasingPattern)]
        public string Pattern { get; set; } = FieldNameChecker.PascalCasingPattern;

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (ClassStatementSyntax)c.Node;
                    if (!FieldNameChecker.IsRegexMatch(declaration.Identifier.ValueText, Pattern))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, declaration.Identifier.GetLocation(), Pattern));
                    }
                },
                SyntaxKind.ClassStatement);
        }
    }
}
