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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ExceptionHandling)]
    [SqaleConstantRemediation("1h")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cwe, Tag.ErrorHandling, Tag.Security)]
    public class CatchEmpty : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2486";
        internal const string Title = "Exceptions should not be ignored";
        internal const string Description =
            "When exceptions occur, it is usually a bad idea to simply ignore them. Instead, it " +
            "is better to handle them properly, or at least to log them.";
        internal const string MessageFormat = "Handle the exception, rather than swallow it with an empty statement.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var catchClause = (CatchClauseSyntax)c.Node;

                    if (!catchClause.Block.Statements.Any())
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation()));
                    }
                },
                SyntaxKind.CatchClause);
        }
    }
}
