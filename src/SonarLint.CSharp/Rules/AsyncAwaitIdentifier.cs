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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class AsyncAwaitIdentifier : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2306";
        internal const string Title = "\"async\" and \"await\" should not be used as identifiers";
        internal const string Description =
            "Since C# 5.0, \"async\" and \"await\" are contextual keywords. Contextual keywords " +
            "do have a particular meaning in some contexts, but can still be used as variable " +
            "names for example. Keywords, on the other hand, are always reserved, and therefore " +
            "are not valid variable names. To avoid any confusion though, it is best to not use " +
            "\"async\" and \"await\" as identifiers.";
        internal const string MessageFormat = "Rename \"{0}\" to not use a contextual keyword as an identifier.";
        internal const string Category = SonarLint.Common.Category.Naming;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        private static readonly IImmutableSet<string> AsyncOrAwait = ImmutableHashSet.Create("async", "await");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    foreach (var asyncOrAwaitToken in GetAsyncOrAwaitTokens(c.Tree.GetRoot())
                        .Where(token => !token.Parent.AncestorsAndSelf().OfType<IdentifierNameSyntax>().Any()))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, asyncOrAwaitToken.GetLocation(), asyncOrAwaitToken.ToString()));
                    }
                });
        }

        private static IEnumerable<SyntaxToken> GetAsyncOrAwaitTokens(SyntaxNode node)
        {
            return node.DescendantTokens()
                .Where(token =>
                    token.IsKind(SyntaxKind.IdentifierToken) &&
                    AsyncOrAwait.Contains(token.ToString()));
        }
    }
}
