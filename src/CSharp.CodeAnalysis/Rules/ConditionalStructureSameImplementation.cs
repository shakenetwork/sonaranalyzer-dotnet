/*
 * SonarQube C# Code Analysis
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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Common;
using SonarQube.CSharp.CodeAnalysis.Common.Sqale;
using SonarQube.CSharp.CodeAnalysis.Helpers;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("bug")]
    public class ConditionalStructureSameImplementation : DiagnosticAnalyzer
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
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major; 
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
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

            context.RegisterSyntaxNodeAction(
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

        private static void CheckStatement(SyntaxNodeAnalysisContext c, StatementSyntax statementToCheck, 
            IEnumerable<StatementSyntax> precedingStatements)
        {
            var precedingStatement = precedingStatements
                .FirstOrDefault(preceding => EquivalenceChecker.AreEquivalent(statementToCheck, preceding));

            if (precedingStatement != null)
            {
                ReportStatement(c, statementToCheck, precedingStatement);
            }
        }

        private static void ReportSection(SyntaxNodeAnalysisContext c, SwitchSectionSyntax switchSection, SwitchSectionSyntax precedingSection)
        {
            ReportSyntaxNode(c, switchSection, precedingSection, "case");
        }

        private static void ReportStatement(SyntaxNodeAnalysisContext c, StatementSyntax statement, StatementSyntax precedingStatement)
        {
            ReportSyntaxNode(c, statement, precedingStatement, "branch");
        }

        private static void ReportSyntaxNode(SyntaxNodeAnalysisContext c, SyntaxNode node, SyntaxNode precedingNode, string errorMessageDiscriminator)
        {
            c.ReportDiagnostic(Diagnostic.Create(
                           Rule,
                           node.GetLocation(),
                           precedingNode.GetLineNumberToReport(), 
                           errorMessageDiscriminator));
        } 
    }
}
