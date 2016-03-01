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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Design, Tag.Suspicious)]
    public class ConditionalStructureSameImplementation : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1871";
        internal const string Title = "Two branches in the same conditional structure should not have exactly the same " +
                                      "implementation";
        internal const string Description =
            "Having two \"cases\" in the same \"switch\" statement or branches in the same " +
            "\"if\" structure with the same implementation is at best duplicate code, and at " +
            "worst a coding error.If the same logic is truly needed for both instances, then " +
            "in an \"if\" structure they should be combined, or for a \"switch\", one should " +
            "fall through to the other.";
        internal const string MessageFormat = "Either merge this {1} with the identical one on line \"{0}\" or change one of the implementations.";
        internal const string Category = SonarLint.Common.Category.Reliability;
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
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var ifStatement = (IfStatementSyntax)c.Node;

                    var precedingStatements = ifStatement
                        .GetPrecedingStatementsInConditionChain()
                        .ToList();

                    CheckStatement(c, ifStatement.Statement, precedingStatements);

                    if (ifStatement.Else == null)
                    {
                        return;
                    }

                    precedingStatements.Add(ifStatement.Statement);
                    CheckStatement(c, ifStatement.Else.Statement, precedingStatements);
                },
                SyntaxKind.IfStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var switchSection = (SwitchSectionSyntax) c.Node;
                    var precedingSection = switchSection
                        .GetPrecedingSections()
                        .FirstOrDefault(
                            preceding => EquivalenceChecker.AreEquivalent(switchSection.Statements, preceding.Statements));

                    if (precedingSection != null)
                    {
                        ReportSection(c, switchSection, precedingSection);
                    }
                },
                SyntaxKind.SwitchSection);
        }

        private static void CheckStatement(SyntaxNodeAnalysisContext context, StatementSyntax statementToCheck,
            IEnumerable<StatementSyntax> precedingStatements)
        {
            var precedingStatement = precedingStatements
                .FirstOrDefault(preceding => EquivalenceChecker.AreEquivalent(statementToCheck, preceding));

            if (precedingStatement != null)
            {
                ReportStatement(context, statementToCheck, precedingStatement);
            }
        }

        private static void ReportSection(SyntaxNodeAnalysisContext context, SwitchSectionSyntax switchSection, SwitchSectionSyntax precedingSection)
        {
            ReportSyntaxNode(context, switchSection, precedingSection, "case");
        }

        private static void ReportStatement(SyntaxNodeAnalysisContext context, StatementSyntax statement, StatementSyntax precedingStatement)
        {
            ReportSyntaxNode(context, statement, precedingStatement, "branch");
        }

        private static void ReportSyntaxNode(SyntaxNodeAnalysisContext context, SyntaxNode node, SyntaxNode precedingNode, string errorMessageDiscriminator)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                           Rule,
                           node.GetLocation(),
                           precedingNode.GetLineNumberToReport(),
                           errorMessageDiscriminator));
        }
    }
}
