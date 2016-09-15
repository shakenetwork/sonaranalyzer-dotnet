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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Unused)]
    public class VariableUnused : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1481";
        internal const string Title = "Unused local variables should be removed";
        internal const string Description =
            "If a local variable is declared but not used, it is dead code and should be removed. " +
            "Doing so will improve maintainability because developers will not wonder what the variable " +
            "is used for.";
        internal const string MessageFormat = "Remove this unused \"{0}\" local variable.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterCodeBlockStartActionInNonGenerated<SyntaxKind>(cbc =>
            {
                var unusedLocals = new List<ISymbol>();

                cbc.RegisterSyntaxNodeAction(c =>
                {
                    unusedLocals.AddRange(
                        ((LocalDeclarationStatementSyntax) c.Node).Declaration.Variables
                            .Select(variable => c.SemanticModel.GetDeclaredSymbol(variable))
                            .Where(symbol => symbol != null));
                },
                SyntaxKind.LocalDeclarationStatement);

                cbc.RegisterSyntaxNodeAction(c =>
                {
                    var symbolsToNotReportOn = GetUsedSymbols(c.Node, c.SemanticModel);
                    foreach (var symbol in symbolsToNotReportOn)
                    {
                        unusedLocals.Remove(symbol);
                    }
                },
                SyntaxKind.IdentifierName);

                cbc.RegisterCodeBlockEndAction(c =>
                {
                    foreach (var unused in unusedLocals)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, unused.Locations.First(), unused.Name));
                    }
                });
            });
        }

        internal static IEnumerable<ISymbol> GetUsedSymbols(SyntaxNode node, SemanticModel semanticModel)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol != null)
            {
                yield return symbolInfo.Symbol;
            }

            foreach (var candidate in symbolInfo.CandidateSymbols.Where(cs => cs != null))
            {
                yield return candidate;
            }
        }
    }
}
