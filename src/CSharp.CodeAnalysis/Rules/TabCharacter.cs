/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
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
using Microsoft.CodeAnalysis.Text;
using SonarQube.CSharp.CodeAnalysis.Common;
using SonarQube.CSharp.CodeAnalysis.Common.Sqale;
using SonarQube.CSharp.CodeAnalysis.Helpers;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Tags("convention")]
    public class TabCharacter : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S105";
        internal const string Title = "Tabulation characters should not be used";
        internal const string Description =
            "Developers should not need to configure the tab width of their text editors in order to be " +
            "able to read source code. So the use of tabulation character must be banned.";
        internal const string MessageFormat = "Replace all tab characters in this file by sequences of white-spaces.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Minor; 
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeAction(
                c =>
                {
                    var offset = c.Tree.GetText().ToString().IndexOf('\t');
                    if (offset < 0)
                    {
                        return;
                    }

                    var location = c.Tree.GetLocation(TextSpan.FromBounds(offset, offset));
                    c.ReportDiagnostic(Diagnostic.Create(Rule, location));
                });
        }
    }
}
