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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug", "cwe")]
    public class ToStringNoNull : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2225";
        internal const string Description = "Assignments should not be made from within sub-expressions";
        internal const string MessageFormat = "Extract the assignment of \"{0}\" from this expression.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2225");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockStartAction<SyntaxKind>(
                cbc =>
                {
                    var methodDeclaration = cbc.CodeBlock as MethodDeclarationSyntax;

                    if (methodDeclaration == null || 
                        methodDeclaration.Identifier.Text != "ToString" ||
                        methodDeclaration.ParameterList.Parameters.Any() ||
                        !methodDeclaration.Modifiers.Select(modifier => modifier.Kind()).Contains(SyntaxKind.OverrideKeyword))
                    {
                        return;
                    }

                    cbc.RegisterSyntaxNodeAction(c =>
                    {
                        var returnStatement = (ReturnStatementSyntax)c.Node;

                        var nullExpression = returnStatement.Expression as LiteralExpressionSyntax;
                        if (nullExpression != null && nullExpression.IsKind(SyntaxKind.NullLiteralExpression))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, returnStatement.GetLocation()));
                        }

                    }, SyntaxKind.ReturnStatement);
                });
        }        
    }
}
