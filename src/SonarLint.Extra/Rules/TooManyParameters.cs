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

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.UnitTestability)]
    [SqaleConstantRemediation("20min")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("brain-overload")]
    public class TooManyParameters : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S107";
        internal const string Description = "Methods should not have too many parameters";
        internal const string MessageFormat = "Method \"{2}\" has {1} parameters, which is greater than the {0} authorized.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major; 
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        [RuleParameter("max", PropertyType.Integer, "Maximum authorized number of parameters", "7")]
        public int Maximum { get; set; }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var parameterListNode = (ParameterListSyntax)c.Node;
                    var parameters = parameterListNode.Parameters.Count;

                    if (parameters > Maximum)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, parameterListNode.GetLocation(), Maximum, parameters, ExtractName(parameterListNode)));
                    }
                },
                SyntaxKind.ParameterList);
        }

        private static string ExtractName(SyntaxNode node)
        {
            string result;
            if (node.IsKind(SyntaxKind.ConstructorDeclaration))
            {
                result = "Constructor \"" + ((ConstructorDeclarationSyntax)node).Identifier + "\"";
            }
            else if (node.IsKind(SyntaxKind.MethodDeclaration))
            {
                result = "Method \"" + ((MethodDeclarationSyntax)node).Identifier + "\"";
            }
            else if (node.IsKind(SyntaxKind.DelegateDeclaration))
            {
                result = "Delegate";
            }
            else
            {
                result = "Lambda";
            }
            return result;
        }
    }
}
