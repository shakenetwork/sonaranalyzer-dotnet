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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("brain-overload")]
    public class MagicNumber : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S109";
        internal const string Description = "Magic numbers should not be used";
        internal const string MessageFormat = "Assign this magic number {0} to a well-named constant, and use the constant instead.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Minor; 
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        [RuleParameter("exceptions", PropertyType.String, "Comma separated list of allowed values (excluding '-' and '+' signs)", "0,1,0x0,0x00,.0,.1,0.0,1.0")]
        public IImmutableSet<string> Exceptions { get; set; }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var literalNode = (LiteralExpressionSyntax)c.Node;

                    if (!literalNode.IsPartOfStructuredTrivia() &&
                        !literalNode.Ancestors().Any(e =>
                          e.IsKind(SyntaxKind.VariableDeclarator) ||
                          e.IsKind(SyntaxKind.EnumDeclaration) ||
                          e.IsKind(SyntaxKind.Attribute)) &&
                        !Exceptions.Contains(literalNode.Token.Text))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, literalNode.GetLocation(), literalNode.Token.Text));
                    }
                },
                SyntaxKind.NumericLiteralExpression);
        }
    }
}
