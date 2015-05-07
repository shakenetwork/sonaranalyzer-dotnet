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

using System;
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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [SqaleConstantRemediation("2min")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("cert", "cwe", "misra", "pitfall")]
    public class UseCurlyBraces : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S121";
        internal const string Description = "Control structures should always use curly braces";
        internal const string MessageFormat = "Add curly braces around the nested statement(s) in this \"{0}\" block.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major; 
        internal const bool IsActivatedByDefault = false;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS121");

        private sealed class CheckedKind
        {
            public SyntaxKind Kind;
            public string Value;
            public Func<SyntaxNode, bool> Validator;
        }

        private readonly ImmutableList<CheckedKind> checkedKinds = ImmutableList.Create(
            new CheckedKind
            {
                Kind = SyntaxKind.IfStatement,
                Value = "if",
                Validator = node => ((IfStatementSyntax)node).Statement.IsKind(SyntaxKind.Block)
            },
            new CheckedKind
            {
                Kind = SyntaxKind.ElseClause,
                Value = "else",
                Validator =
                    node =>
                    {
                        var statement = ((ElseClauseSyntax)node).Statement;
                        return statement.IsKind(SyntaxKind.IfStatement) || statement.IsKind(SyntaxKind.Block);
                    }
            },
            new CheckedKind
            {
                Kind = SyntaxKind.ForStatement,
                Value = "for",
                Validator = node => ((ForStatementSyntax)node).Statement.IsKind(SyntaxKind.Block)
            },
            new CheckedKind
            {
                Kind = SyntaxKind.ForEachStatement,
                Value = "foreach",
                Validator = node => ((ForEachStatementSyntax)node).Statement.IsKind(SyntaxKind.Block)
            },
            new CheckedKind
            {
                Kind = SyntaxKind.DoStatement,
                Value = "do",
                Validator = node => ((DoStatementSyntax)node).Statement.IsKind(SyntaxKind.Block)
            },
            new CheckedKind
            {
                Kind = SyntaxKind.WhileStatement,
                Value = "while",
                Validator = node => ((WhileStatementSyntax)node).Statement.IsKind(SyntaxKind.Block)
            });

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var checkedKind = checkedKinds.Single(e => c.Node.IsKind(e.Kind));

                    if (!checkedKind.Validator(c.Node))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation(), checkedKind.Value));
                    }
                },
                checkedKinds.Select(e => e.Kind).ToArray());
        }
    }
}
