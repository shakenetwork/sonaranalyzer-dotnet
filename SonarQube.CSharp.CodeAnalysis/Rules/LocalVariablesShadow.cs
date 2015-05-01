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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("pitfall")]
    public class LocalVariablesShadow : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1117";
        internal const string Description = "Local variables should not shadow class fields";
        internal const string MessageFormat = "Rename \"{0}\" which hides the {1} with the same name.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS1117");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var method = (MethodDeclarationSyntax) c.Node;

                    if (method.Modifiers.Select(m => m.Kind()).Contains(SyntaxKind.StaticKeyword))
                    {
                        return;
                    }

                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(method);
                    if (methodSymbol == null)
                    {
                        return;
                    }

                    var parameters = method.ParameterList.Parameters.ToList();
                    var members = GetMembers(methodSymbol.ContainingType);

                    foreach (var parameter in parameters)
                    {
                        CheckMatch(members, parameter.Identifier, c);
                    }
                },
                SyntaxKind.MethodDeclaration);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var declaration = (LocalDeclarationStatementSyntax) c.Node;
                    var variables = declaration.Declaration.Variables;

                    List<ISymbol> members = null;
                    foreach (var variable in variables)
                    {
                        var variableSymbol = c.SemanticModel.GetDeclaredSymbol(variable);
                        if (variableSymbol == null)
                        {
                            return;
                        }

                        if (members == null)
                        {
                            members = GetMembers(variableSymbol.ContainingType);
                        }
                        CheckMatch(members, variable.Identifier, c);
                    }
                },
                SyntaxKind.LocalDeclarationStatement);
        }

        private static void CheckMatch(IEnumerable<ISymbol> members, SyntaxToken identifier, SyntaxNodeAnalysisContext c)
        {
            var matchingMember = members.FirstOrDefault(m => m.Name == identifier.Text);

            if (matchingMember != null)
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), 
                    identifier.Text,
                    (matchingMember is IFieldSymbol) ? "field" : "property"));
            }
        }

        private static List<ISymbol> GetMembers(INamedTypeSymbol classSymbol)
        {
            return classSymbol.GetMembers()
                .Where(member => member is IFieldSymbol || member is IPropertySymbol)
                .ToList();
        }
    }
}
