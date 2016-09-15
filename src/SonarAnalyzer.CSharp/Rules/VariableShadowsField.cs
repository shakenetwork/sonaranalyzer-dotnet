/*
 * SonarAnalyzer for .NET
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
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class VariableShadowsField : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1117";
        internal const string Title = "Local variables should not shadow class fields";
        internal const string Description =
            "Shadowing fields with a local variable is a bad practice that reduces code readability: It makes it confusing to know whether the field or the " +
            "variable is being used.";
        internal const string MessageFormat = "Rename \"{0}\" which hides the {1} with the same name.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
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
                    var declaration = (ForEachStatementSyntax)c.Node;

                    var variableSymbol = c.SemanticModel.GetDeclaredSymbol(declaration);
                    if (variableSymbol == null)
                    {
                        return;
                    }

                    var members = GetMembers(variableSymbol.ContainingType);

                    ReportOnVariableMatchingField(members, declaration.Identifier, c);
                },
                SyntaxKind.ForEachStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ProcessStatementWithVariableDeclaration((LocalDeclarationStatementSyntax)c.Node, s => s.Declaration, c),
                SyntaxKind.LocalDeclarationStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ProcessStatementWithVariableDeclaration((ForStatementSyntax)c.Node, s => s.Declaration, c),
                SyntaxKind.ForStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ProcessStatementWithVariableDeclaration((UsingStatementSyntax)c.Node, s => s.Declaration, c),
                SyntaxKind.UsingStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ProcessStatementWithVariableDeclaration((FixedStatementSyntax)c.Node, s => s.Declaration, c),
                SyntaxKind.FixedStatement);
        }

        private static void ProcessStatementWithVariableDeclaration<T>(T declaration, Func<T, VariableDeclarationSyntax> variableSelector,
            SyntaxNodeAnalysisContext context)
        {
            var variableDeclaration = variableSelector(declaration);
            if (variableDeclaration == null)
            {
                return;
            }

            var variables = variableDeclaration.Variables;

            List<ISymbol> members = null;
            foreach (var variable in variables)
            {
                var variableSymbol = context.SemanticModel.GetDeclaredSymbol(variable);
                if (variableSymbol == null)
                {
                    return;
                }

                if (members == null)
                {
                    members = GetMembers(variableSymbol.ContainingType);
                }

                ReportOnVariableMatchingField(members, variable.Identifier, context);
            }
        }

        private static void ReportOnVariableMatchingField(IEnumerable<ISymbol> members, SyntaxToken identifier, SyntaxNodeAnalysisContext context)
        {
            var matchingMember = members.FirstOrDefault(m => m.Name == identifier.ValueText);
            if (matchingMember == null)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(),
                identifier.Text,
                (matchingMember is IFieldSymbol) ? "field" : "property"));
        }

        private static List<ISymbol> GetMembers(INamedTypeSymbol classSymbol)
        {
            return classSymbol.GetMembers()
                .Where(member => member is IFieldSymbol || member is IPropertySymbol)
                .ToList();
        }
    }
}
