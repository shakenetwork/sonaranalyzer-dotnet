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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Common;
using SonarQube.CSharp.CodeAnalysis.Common.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("1h")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Tags("brain-overload")]
    public class FileLines : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S104";
        internal const string Description = "Files should not have too many lines";
        internal const string MessageFormat = "This file has {1} lines, which is greater than {0} authorized. Split it into smaller files.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major; 
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        [RuleParameter("maximumFileLocThreshold", PropertyType.Integer, "Maximum authorized lines in a file.", "1000")]
        public int Maximum { get; set; }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeAction(
                c =>
                {
                    var root = c.Tree.GetRoot();
                    var lines = root.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

                    if (lines > Maximum)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, Location.None, Maximum, lines));
                    }
                });
        }
    }
}
