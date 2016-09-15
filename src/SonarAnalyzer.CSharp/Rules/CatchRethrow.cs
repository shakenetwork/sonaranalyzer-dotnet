/*
 * SonarAnalyzer for .NET
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
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Linq;
using System.Collections.Generic;
using System;
using SonarAnalyzer.Helpers.CSharp;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    [Tags(Tag.Clumsy, Tag.Unused, Tag.Finding)]
    public class CatchRethrow : SonarDiagnosticAnalyzer
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
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        private static readonly BlockSyntax ThrowBlock = SyntaxFactory.Block(SyntaxFactory.ThrowStatement());

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), true,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var tryStatement = (TryStatementSyntax)c.Node;
                    var catches = tryStatement.Catches.ToList();
                    var caughtExceptionTypes = new Lazy<List<INamedTypeSymbol>>(() => ComputeExceptionTypesIfNeeded(catches, c.SemanticModel));
                    var redundantCatches = new HashSet<CatchClauseSyntax>();
                    var isIntermediate = false;

                    for (int i = catches.Count - 1; i >= 0; i--)
                    {
                        var currentCatch = catches[i];
                        if (!EquivalenceChecker.AreEquivalent(currentCatch.Block, ThrowBlock))
                        {
                            isIntermediate = true;
                            continue;
                        }

                        if (!isIntermediate)
                        {
                            redundantCatches.Add(currentCatch);
                            continue;
                        }

                        if (currentCatch.Filter != null)
                        {
                            continue;
                        }

                        if (!IsMoreSpecificTypeThanANotRedundantCatch(i, catches, caughtExceptionTypes.Value, redundantCatches))
                        {
                            redundantCatches.Add(currentCatch);
                        }
                    }

                    foreach (var redundantCatch in redundantCatches)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, redundantCatch.GetLocation()));
                    }
                },
                SyntaxKind.TryStatement);
        }

        private static bool IsMoreSpecificTypeThanANotRedundantCatch(int catchIndex, List<CatchClauseSyntax> catches, List<INamedTypeSymbol> caughtExceptionTypes,
            HashSet<CatchClauseSyntax> redundantCatches)
        {
            var currentType = caughtExceptionTypes[catchIndex];
            for (int i = catchIndex + 1; i < caughtExceptionTypes.Count; i++)
            {
                var followingType = caughtExceptionTypes[i];

                if (followingType == null ||
                    currentType.DerivesOrImplements(followingType))
                {
                    return !redundantCatches.Contains(catches[i]);
                }
            }
            return false;
        }

        private static List<INamedTypeSymbol> ComputeExceptionTypesIfNeeded(IEnumerable<CatchClauseSyntax> catches, SemanticModel semanticModel)
        {
            return catches
                .Select(clause =>
                    clause.Declaration?.Type != null
                        ? semanticModel.GetTypeInfo(clause.Declaration?.Type).Type as INamedTypeSymbol
                        : null)
                .ToList();
        }
    }
}
