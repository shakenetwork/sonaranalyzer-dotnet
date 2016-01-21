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
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using SonarLint.Common;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    public abstract class TabCharacterBase : DiagnosticAnalyzer, IMultiLanguageDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S105";
        protected const string Title = "Tabulation characters should not be used";
        protected const string Description =
            "Developers should not need to configure the tab width of their text editors in order to be " +
            "able to read source code. So the use of tabulation character must be banned.";
        protected const string MessageFormat = "Replace all tab characters in this file by sequences of white-spaces.";
        protected const string Category = SonarLint.Common.Category.Maintainability;
        protected const Severity RuleSeverity = Severity.Minor;
        protected const bool IsActivatedByDefault = false;

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                GeneratedCodeRecognizer,
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

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }
        GeneratedCodeRecognizer IMultiLanguageDiagnosticAnalyzer.GeneratedCodeRecognizer => GeneratedCodeRecognizer;
    }
}
