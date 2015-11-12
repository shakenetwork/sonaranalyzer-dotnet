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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("1min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags(Tag.Convention)]
    public class LineLength : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S103";
        internal const string Description = "Lines should not be too long";
        internal const string MessageFormat = "Split this {1} characters long line (which is greater than {0} authorized).";
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const int DefaultValueMaximum = 200;

        [RuleParameter("maximumLineLength", PropertyType.Integer, "The maximum authorized line length.", DefaultValueMaximum)]
        public int Maximum { get; set; } = DefaultValueMaximum;

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    foreach (var line in c.Tree.GetText().Lines
                        .Where(line => line.Span.Length > Maximum))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, c.Tree.GetLocation(line.Span), Maximum, line.Span.Length));
                    }
                });
        }
    }
}
