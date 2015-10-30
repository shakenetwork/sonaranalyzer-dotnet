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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;
using System.Collections.Generic;

namespace SonarLint.Rules
{
    public abstract class FieldShouldNotBePublicBase : DiagnosticAnalyzer
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
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected static bool FieldIsRelevant(IFieldSymbol fieldSymbol)
        {
            return fieldSymbol != null &&
                   !fieldSymbol.IsStatic &&
                   !fieldSymbol.IsConst &&
                   fieldSymbol.DeclaredAccessibility == Accessibility.Public;
        }
    }

    public abstract class FieldShouldNotBePublicBase<TLanguageKindEnum, TFieldDeclarationSyntax, TVariableSyntax> : FieldShouldNotBePublicBase
        where TLanguageKindEnum : struct
        where TFieldDeclarationSyntax : SyntaxNode
        where TVariableSyntax : SyntaxNode
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                    c =>
                    {
                        var fieldDeclaration = (TFieldDeclarationSyntax)c.Node;
                        var variables = GetVariables(fieldDeclaration);

                        foreach (var variable in variables
                            .Select(variableDeclaratorSyntax => new
                            {
                                Syntax = variableDeclaratorSyntax,
                                Symbol = c.SemanticModel.GetDeclaredSymbol(variableDeclaratorSyntax) as IFieldSymbol
                            })
                            .Where(f => FieldIsRelevant(f.Symbol)))
                        {
                            var identifier = GetIdentifier(variable.Syntax);
                            c.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(),
                                identifier.ValueText));
                        }
                    },
                    SyntaxKindsOfInterest.ToArray());
        }

        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
        protected abstract IEnumerable<TVariableSyntax> GetVariables(TFieldDeclarationSyntax fieldDeclaration);
        protected abstract SyntaxToken GetIdentifier(TVariableSyntax variable);
    }
}
