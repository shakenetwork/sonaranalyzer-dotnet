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

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("pitfall")]
    public class VariableShadowsField : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1117";
        internal const string Title = "Local variables should not shadow class fields";
        internal const string Description =
            "Shadowing fields with a local variable or with a method parameter is a bad practice " +
            "that reduces code readability: It makes it confusing to know whether the field or the " +
            "variable is being used.";
        internal const string MessageFormat = "Rename \"{0}\" which hides the {1} with the same name.";
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
