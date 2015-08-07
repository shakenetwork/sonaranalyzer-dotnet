/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("unused")]
    public class RedundantParentheses : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3235";
        internal const string Title = "Redundant parentheses should not be used";
        internal const string Description =
            "Redundant parentheses are simply wasted keystrokes, and should be removed.";
        internal const string MessageFormat = "Remove these redundant parentheses.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var argumentList = (AttributeArgumentListSyntax)c.Node;
                    if (!argumentList.Arguments.Any())
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, argumentList.GetLocation()));
                    }
                },
                SyntaxKind.AttributeArgumentList);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var objectCreation = (ObjectCreationExpressionSyntax)c.Node;
                    var argumentList = objectCreation.ArgumentList;
                    if (argumentList != null &&
                        objectCreation.Initializer != null &&
                        !argumentList.Arguments.Any())
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, argumentList.GetLocation()));
                    }
                },
                SyntaxKind.ObjectCreationExpression);
        }
    }
}
