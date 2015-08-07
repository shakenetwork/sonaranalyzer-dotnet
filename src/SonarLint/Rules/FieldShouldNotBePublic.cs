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
    [SqaleConstantRemediation("30min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureChangeability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("pitfall")]
    public class FieldShouldNotBePublic : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2357";
        internal const string Title = "Fields should be private";
        internal const string Description =
            "Fields should not be part of an API, and therefore should always be private. Indeed, they " +
            "cannot be added to an interface for instance, and validation cannot be added later on without " +
            "breaking backward compatiblity. Instead, developers should encapsulate their fields into " +
            "properties. Explicit property getters and setters can be introduced for validation purposes " +
            "or to smooth the transition to a newer system.";
        internal const string MessageFormat = "Make \"{0}\" private.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var fieldDeclaration = (FieldDeclarationSyntax) c.Node;
                    foreach (var field in fieldDeclaration.Declaration.Variables
                        .Select(variableDeclaratorSyntax => new
                        {
                            Syntax = variableDeclaratorSyntax,
                            Symbol = c.SemanticModel.GetDeclaredSymbol(variableDeclaratorSyntax) as IFieldSymbol
                        })
                        .Where(f => FieldIsRelevant(f.Symbol)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, field.Syntax.Identifier.GetLocation(),
                            field.Syntax.Identifier.ValueText));
                    }

                },
                SyntaxKind.FieldDeclaration);
        }

        private static bool FieldIsRelevant(IFieldSymbol fieldSymbol)
        {
            return fieldSymbol != null &&
                   !fieldSymbol.IsStatic &&
                   !fieldSymbol.IsConst &&
                   fieldSymbol.DeclaredAccessibility == Accessibility.Public;
        }
    }
}
