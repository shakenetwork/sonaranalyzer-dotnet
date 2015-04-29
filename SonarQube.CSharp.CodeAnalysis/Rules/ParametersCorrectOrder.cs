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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug")]
    public class ParametersCorrectOrder : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2234";
        internal const string Description = "Parameters should be passed in the correct order";
        internal const string MessageFormat = "Parameters to \"{0}\" have the same names but not the same order as the method arguments.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Blocker;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2234");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var methodCall = (InvocationExpressionSyntax) c.Node;

                    var methodSymbol = c.SemanticModel.GetSymbolInfo(methodCall).Symbol as IMethodSymbol;

                    if (methodSymbol == null)
                    {
                        return;
                    }

                    var methodDeclaration = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();

                    if (methodDeclaration == null)
                    {
                        return;
                    }

                    var methodDeclarationSyntax = methodDeclaration.GetSyntax() as MethodDeclarationSyntax;

                    if (methodDeclarationSyntax == null)
                    {
                        return;
                    }

                    var namesInDeclaration = methodDeclarationSyntax.ParameterList.Parameters
                        .Select(parameter => parameter.Identifier.Text)
                        .ToList();

                    var memberAccess = methodCall.Expression as MemberAccessExpressionSyntax;

                    if (methodSymbol.IsExtensionMethod &&
                        memberAccess != null)
                    {
                        var symbol = c.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol as ITypeSymbol;
                        if (symbol == null || !symbol.IsType)
                        {
                            namesInDeclaration = namesInDeclaration.Skip(1).ToList();
                        }
                    }

                    var parametersInCall = GetParametersForCall(methodCall);
                    var namesInCall = parametersInCall
                        .Select(p => p.IdentifierName)
                        .ToList();

                    if (!namesInDeclaration.Intersect(namesInCall).Any())
                    {
                        return;
                    }

                    var methodCallHasIssue = false;

                    for (var i = 0; i < parametersInCall.Count; i++)
                    {
                        if (string.IsNullOrEmpty(namesInCall[i]) ||
                            !namesInDeclaration.Contains(namesInCall[i]))
                        {
                            continue;
                        }

                        var positional = parametersInCall[i] as PositionalIdentifierParameter;
                        if (positional != null && (!namesInCall.Contains(namesInDeclaration[i]) || namesInCall[i] == namesInDeclaration[i]))
                        {
                            continue;
                        }

                        var named = parametersInCall[i] as NamedIdentifierParameter;
                        if (named != null && (!namesInCall.Contains(named.DeclaredName) || named.DeclaredName == named.IdentifierName))
                        {
                            continue;
                        }

                        methodCallHasIssue = true;
                        break;
                    }

                    if (methodCallHasIssue)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, methodCall.GetLocation(), methodDeclarationSyntax.Identifier.Text));
                    }
                },
                SyntaxKind.InvocationExpression);
        }
        
        private static List<IdentifierParameter> GetParametersForCall(InvocationExpressionSyntax methodCall)
        {
            return methodCall.ArgumentList.Arguments.ToList()
                .Select((argument, index) =>
                {
                    var identifier = argument.Expression as IdentifierNameSyntax;
                    var identifierName = identifier == null ? (string) null : identifier.Identifier.Text;

                    IdentifierParameter parameter;
                    if (argument.NameColon == null)
                    {
                        parameter = new PositionalIdentifierParameter()
                        {
                            IdentifierName = identifierName,
                            Position = index
                        };
                    }
                    else
                    {
                        parameter = new NamedIdentifierParameter()
                        {
                            IdentifierName = identifierName,
                            DeclaredName = argument.NameColon.Name.Identifier.Text
                        };
                    }
                    return parameter;
                })
                .ToList();
        }

        internal class IdentifierParameter
        {
            public string IdentifierName { get; set; }
        }
        internal class PositionalIdentifierParameter : IdentifierParameter
        {
            public int Position { get; set; }
        }
        internal class NamedIdentifierParameter : IdentifierParameter
        {
            public string DeclaredName { get; set; }
        }
    }
}
