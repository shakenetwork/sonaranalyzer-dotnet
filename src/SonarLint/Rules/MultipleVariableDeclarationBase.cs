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
using System.Collections.Generic;

namespace SonarLint.Rules.Common
{
    public abstract class MultipleVariableDeclarationBase : SonarDiagnosticAnalyzer, IMultiLanguageDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1659";
        protected const string Title = "Multiple variables should not be declared on the same line";
        protected const string Description =
            "Declaring multiple variable on one line is difficult to read.";
        protected const string MessageFormat = "Declare \"{0}\" in a separate statement.";
        protected const string Category = SonarLint.Common.Category.Maintainability;
        protected const Severity RuleSeverity = Severity.Minor;
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

    public abstract class MultipleVariableDeclarationBase<TLanguageKindEnum,
        TFieldDeclarationSyntax, TLocalDeclarationSyntax> : MultipleVariableDeclarationBase
        where TLanguageKindEnum : struct
        where TFieldDeclarationSyntax: SyntaxNode
        where TLocalDeclarationSyntax: SyntaxNode
    {
        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var local = (TLocalDeclarationSyntax)c.Node;
                    CheckAndReportVariables(GetIdentifiers(local).ToList(), c);
                },
                LocalDeclarationKind);

            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var field = (TFieldDeclarationSyntax)c.Node;
                    CheckAndReportVariables(GetIdentifiers(field).ToList(), c);
                },
                FieldDeclarationKind);
        }

        private static void CheckAndReportVariables(IList<SyntaxToken> variables, SyntaxNodeAnalysisContext context)
        {
            if (variables.Count <= 1)
            {
                return;
            }
            foreach (var variable in variables.Skip(1))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, variable.GetLocation(), variable.ValueText));
            }
        }

        protected abstract IEnumerable<SyntaxToken> GetIdentifiers(TLocalDeclarationSyntax node);
        protected abstract IEnumerable<SyntaxToken> GetIdentifiers(TFieldDeclarationSyntax node);

        public abstract TLanguageKindEnum LocalDeclarationKind { get; }
        public abstract TLanguageKindEnum FieldDeclarationKind { get; }
    }
}
