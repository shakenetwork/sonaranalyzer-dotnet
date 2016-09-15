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
    public class EventHandlerName : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2347";
        internal const string Title = "Event handlers should comply with a naming convention";
        internal const string Description =
            "Shared coding conventions allow teams to collaborate efficiently. " +
            "This rule checks that all even handler names match a provided regular expression.";
        internal const string MessageFormat = "Rename event handler \"{0}\" to match the regular expression: \"{1}\".";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), false,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        private const string DefaultPattern = "^(([a-z][a-z0-9]*)?" + FieldNameChecker.PascalCasingInternalPattern + "_)?" +
            FieldNameChecker.PascalCasingInternalPattern + "$";

        [RuleParameter("format", PropertyType.String,
            "Regular expression used to check the even handler names against.", DefaultPattern)]
        public string Pattern { get; set; } = DefaultPattern;

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var methodDeclaration = (MethodStatementSyntax)c.Node;
                    if (!FieldNameChecker.IsRegexMatch(methodDeclaration.Identifier.ValueText, Pattern) &&
                        IsEventHandler(methodDeclaration, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(),
                            methodDeclaration.Identifier.ValueText, Pattern));
                    }
                },
                SyntaxKind.SubStatement);
        }

        internal static bool IsEventHandler(MethodStatementSyntax declaration, SemanticModel semanticModel)
        {
            if (declaration.HandlesClause != null)
            {
                return true;
            }

            var symbol = semanticModel.GetDeclaredSymbol(declaration);
            return symbol != null &&
                symbol.IsProbablyEventHandler();
        }
    }
}
