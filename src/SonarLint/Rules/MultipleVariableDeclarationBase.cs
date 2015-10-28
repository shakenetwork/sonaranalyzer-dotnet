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
using SonarLint.Helpers;
using System.Collections.Generic;

namespace SonarLint.Rules.Common
{
    public abstract class MultipleVariableDeclarationBase : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1659";
        internal const string Title = "Multiple variables should not be declared on the same line";
        internal const string Description =
            "Declaring multiple variable on one line is difficult to read.";
        internal const string MessageFormat = "Declare \"{0}\" in a separate statement.";
        internal const string Category = "SonarLint";
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }
    }

    public abstract class MultipleVariableDeclarationBase<TLanguageKindEnum,
        TFieldDeclarationSyntax, TLocalDeclarationSyntax> : MultipleVariableDeclarationBase
        where TLanguageKindEnum : struct
        where TFieldDeclarationSyntax: SyntaxNode
        where TLocalDeclarationSyntax: SyntaxNode
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var local = (TLocalDeclarationSyntax)c.Node;
                    CheckAndReportVariables(GetIdentifiers(local), c);
                },
                LocalDeclarationKind);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var field = (TFieldDeclarationSyntax)c.Node;
                    CheckAndReportVariables(GetIdentifiers(field), c);
                },
                FieldDeclarationKind);
        }

        private static void CheckAndReportVariables(IEnumerable<SyntaxToken> variables, SyntaxNodeAnalysisContext c)
        {
            if (variables.Count() <= 1)
            {
                return;
            }
            foreach (var variable in variables.Skip(1))
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, variable.GetLocation(), variable.ValueText));
            }
        }

        protected abstract IEnumerable<SyntaxToken> GetIdentifiers(TLocalDeclarationSyntax node);
        protected abstract IEnumerable<SyntaxToken> GetIdentifiers(TFieldDeclarationSyntax node);

        public abstract TLanguageKindEnum LocalDeclarationKind { get; }
        public abstract TLanguageKindEnum FieldDeclarationKind { get; }
    }
}
