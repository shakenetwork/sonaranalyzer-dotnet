/*
 * SonarQube C# Code Analysis
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
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Common;
using SonarQube.CSharp.CodeAnalysis.Common.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("convention")]
    public class ClassName : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S101";
        internal const string Description = "Class name should comply with a naming convention";
        internal const string MessageFormat = "Rename this class \"{1}\" to match the regular expression: {0}";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        [RuleParameter("format", PropertyType.String, "Regular expression used to check the class names against.", "^(?:[A-HJ-Z][a-zA-Z0-9]+|I[a-z0-9][a-zA-Z0-9]*)$")]
        public string Convention { get; set; }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var identifier = ((ClassDeclarationSyntax)c.Node).Identifier;

                    if (!Regex.IsMatch(identifier.Text, Convention))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), Convention, identifier.Text));
                    }
                },
                SyntaxKind.ClassDeclaration);
        }
    }
}
