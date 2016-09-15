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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, true)]
    [Tags(Tag.Convention)]
    public class FunctionName : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1542";
        internal const string Title = "Function names should comply with a naming convention";
        internal const string Description =
            "Shared naming conventions allow teams to collaborate efficiently. " +
            "This rule checks that all subroutine and function names match a provided regular expression.";
        internal const string MessageFormat = "Rename {0} \"{1}\" to match the regular expression: \"{2}\".";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), false,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        [RuleParameter("format", PropertyType.String,
            "Regular expression used to check the function names against.", FieldNameChecker.PascalCasingPattern)]
        public string Pattern { get; set; } = FieldNameChecker.PascalCasingPattern;

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var methodDeclaration = (MethodStatementSyntax)c.Node;
                    if (!FieldNameChecker.IsRegexMatch(methodDeclaration.Identifier.ValueText, Pattern))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(),
                            "function", methodDeclaration.Identifier.ValueText, Pattern));
                    }
                },
                SyntaxKind.FunctionStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var methodDeclaration = (MethodStatementSyntax)c.Node;
                    if (!FieldNameChecker.IsRegexMatch(methodDeclaration.Identifier.ValueText, Pattern) &&
                        !EventHandlerName.IsEventHandler(methodDeclaration, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(),
                            "procedure", methodDeclaration.Identifier.ValueText, Pattern));
                    }
                },
                SyntaxKind.SubStatement);
        }
    }
}
