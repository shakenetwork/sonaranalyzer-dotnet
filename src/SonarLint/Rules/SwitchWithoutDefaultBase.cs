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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;

namespace SonarLint.Rules.Common
{
    public abstract class SwitchWithoutDefaultBase : DiagnosticAnalyzer, IMultiLanguageDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S131";
        protected const string Title = "\"switch/Select\" statements should end with a \"default/Case Else\" clause";
        protected const string Description =
            "The requirement for a final \"default/Case Else\" clause is defensive programming. The clause should either " +
            "take appropriate action, or contain a suitable comment as to why no action is taken. Even when the " +
            "\"switch/Select\" covers all current values of an enumeration, a \"default/Case Else\" case should still be used because " +
            "there is no guarantee that the enumeration won't be extended.";
        protected const string MessageFormat = "Add a \"{0}\" clause to this \"{1}\" statement.";
        protected const string Category = SonarLint.Common.Category.Reliability;
        protected const Severity RuleSeverity = Severity.Major;
        protected const bool IsActivatedByDefault = false;

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }
        GeneratedCodeRecognizer IMultiLanguageDiagnosticAnalyzer.GeneratedCodeRecognizer => GeneratedCodeRecognizer;
    }

    public abstract class SwitchWithoutDefaultBase<TLanguageKindEnum> : SwitchWithoutDefaultBase
        where TLanguageKindEnum : struct
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    Diagnostic diagnostic;
                    if (TryGetDiagnostic(c.Node, out diagnostic))
                    {
                        c.ReportDiagnostic(diagnostic);
                    }
                },
                SyntaxKindsOfInterest.ToArray());
        }

        protected abstract bool TryGetDiagnostic(SyntaxNode node, out Diagnostic diagnostic);

        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
    }
}
