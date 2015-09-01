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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy, Tag.Unused)]
    public class CatchRethrow : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2737";
        internal const string Title = "\"catch\" clauses should do more than rethrow";
        internal const string Description =
            "A \"catch\" clause that only rethrows the caught exception has the same effect " +
            "as omitting the \"catch\" altogether and letting it bubble up automatically, but " +
            "with more code and the additional detrement of leaving maintainers scratching " +
            "their heads. Such clauses should either be eliminated or populated with the " +
            "appropriate logic.";
        internal const string MessageFormat = @"Add logic to this catch clause or eliminate it and rethrow the exception automatically.";
        internal const string Category = "SonarLint";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        private static readonly BlockSyntax ThrowBlock = SyntaxFactory.Block(SyntaxFactory.ThrowStatement());

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var tryStatement = (TryStatementSyntax)c.Node;

                    var lastCatchClause = tryStatement.Catches.LastOrDefault();

                    if (lastCatchClause!=null &&
                        EquivalenceChecker.AreEquivalent(lastCatchClause.Block, ThrowBlock))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(
                                Rule,
                                lastCatchClause.GetLocation()));
                    }
                },
                SyntaxKind.TryStatement);
        }
    }
}
