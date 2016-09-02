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
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System;
using System.Text.RegularExpressions;

namespace SonarLint.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Convention)]
    public class PublicSharedReadonlyFieldName : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2370";
        internal const string Title = "Non-private \"Shared ReadOnly\" fields should comply with a naming convention";
        internal const string Description =
            "Shared coding conventions allow teams to collaborate efficiently. This rule checks that all " +
            "\"Public Shared ReadOnly\" field names comply with the provided regular expression.";
        internal const string MessageFormat = "Rename \"{0}\" to match the regular expression: \"{1}\".";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        private const string DefaultPattern = "^([A-Z]{1,3}[a-z0-9]+)*([A-Z]{2})?$";

        [RuleParameter("format", PropertyType.String,
            "Regular expression used to check the non-private \"Shared ReadOnly\" field names against.", DefaultPattern)]
        public string Pattern { get; set; } = DefaultPattern;

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var fieldDeclaration = (FieldDeclarationSyntax)c.Node;
                    foreach (var name in fieldDeclaration.Declarators.SelectMany(v => v.Names))
                    {
                        var symbol = c.SemanticModel.GetDeclaredSymbol(name) as IFieldSymbol;
                        if (IsCandidateSymbol(symbol) &&
                            !IsRegexMatch(symbol.Name, Pattern))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), symbol.Name, Pattern));
                        }
                    }
                },
                SyntaxKind.FieldDeclaration);
        }

        private static bool IsRegexMatch(string name, string pattern)
        {
            return Regex.IsMatch(name, pattern);
        }

        private static bool IsCandidateSymbol(IFieldSymbol symbol)
        {
            return symbol != null &&
                symbol.DeclaredAccessibility != Accessibility.Private &&
                symbol.IsShared() &&
                symbol.IsReadOnly;
        }
    }
}
