/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
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
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("unused")]
    public class UnusedLocalVariable : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1481";
        internal const string Description = "Unused local variables should be removed";
        internal const string MessageFormat = "Remove this unused \"{0}\" local variable.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major; 
        internal const bool IsActivatedByDefault = true;
        
        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS1481");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }
        
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockStartAction<SyntaxKind>(cbc =>
            {
                var unusedLocals = new List<ISymbol>();
 
                cbc.RegisterSyntaxNodeAction(c =>
                {
                    unusedLocals.AddRange(
                        ((LocalDeclarationStatementSyntax) c.Node).Declaration
                            .Variables.Select(variable => c.SemanticModel.GetDeclaredSymbol(variable)));
                },
                SyntaxKind.LocalDeclarationStatement);

                cbc.RegisterSyntaxNodeAction(c =>
                {
                    var variableDeclarationSyntax = ((UsingStatementSyntax)c.Node).Declaration;
                    if (variableDeclarationSyntax != null)
                    {
                        unusedLocals.AddRange(
                            variableDeclarationSyntax
                                .Variables.Select(variable => c.SemanticModel.GetDeclaredSymbol(variable)));
                    }
                },
                SyntaxKind.UsingStatement);
 
                cbc.RegisterSyntaxNodeAction(c =>
                {
                    var symbolInfo = c.SemanticModel.GetSymbolInfo(c.Node);
                    unusedLocals.Remove(symbolInfo.Symbol);

                    foreach (var candidateSymbol in symbolInfo.CandidateSymbols)
                    {
                        unusedLocals.Remove(candidateSymbol);
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
    }
}
