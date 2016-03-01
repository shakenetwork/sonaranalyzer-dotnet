/*
 * SonarLint for Visual Studio
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

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;
using System.Collections.Generic;

namespace SonarLint.Rules
{
    public abstract class FieldShouldNotBePublicBase : SonarDiagnosticAnalyzer, IMultiLanguageDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S2357";
        protected const string Title = "Fields should be private";
        protected const string Description =
            "Fields should not be part of an API, and therefore should always be private. Indeed, they " +
            "cannot be added to an interface for instance, and validation cannot be added later on without " +
            "breaking backward compatiblity. Instead, developers should encapsulate their fields into " +
            "properties. Explicit property getters and setters can be introduced for validation purposes " +
            "or to smooth the transition to a newer system.";
        protected const string MessageFormat = "Make \"{0}\" private.";
        protected const string Category = SonarLint.Common.Category.Design;
        protected const Severity RuleSeverity = Severity.Major;
        protected const bool IsActivatedByDefault = false;

        protected static readonly DiagnosticDescriptor Rule =
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

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }
        GeneratedCodeRecognizer IMultiLanguageDiagnosticAnalyzer.GeneratedCodeRecognizer => GeneratedCodeRecognizer;
    }

    public abstract class FieldShouldNotBePublicBase<TLanguageKindEnum, TFieldDeclarationSyntax, TVariableSyntax> : FieldShouldNotBePublicBase
        where TLanguageKindEnum : struct
        where TFieldDeclarationSyntax : SyntaxNode
        where TVariableSyntax : SyntaxNode
    {
        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
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
