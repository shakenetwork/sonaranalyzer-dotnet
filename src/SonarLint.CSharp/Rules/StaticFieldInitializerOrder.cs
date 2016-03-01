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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("bug")]
    public class StaticFieldInitializerOrder : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3263";
        internal const string Title = "Static fields should appear in the order they must be initialized";
        internal const string Description =
            "Static field initializers are executed in the order in which they appear in the class from top to bottom. " +
            "Thus, placing a static field in a class above the field or fields required for its initialization will yield " +
            "unexpected results.";
        internal const string MessageFormat = "Move this field's initializer into a static constructor.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Blocker;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var fieldDeclaration = (FieldDeclarationSyntax)c.Node;
                    var variables = fieldDeclaration.Declaration.Variables;
                    var classDeclaration = fieldDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();

                    if (!fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) ||
                        !variables.Any())
                    {
                        return;
                    }

                    var containingType = c.SemanticModel.GetDeclaredSymbol(variables.First()).ContainingType;

                    foreach (var variable in variables.Where(v => v.Initializer != null))
                    {
                        var identifierFieldMappings = GetIdentifierFieldMappings(variable, containingType, c.SemanticModel);
                        var identifierClassMappings = GetIdentifierClassMappings(identifierFieldMappings);

                        var usedClassDeclarations = identifierClassMappings
                            .Select(mapping => mapping.ClassDeclaration);
                        var isAnyInDifferentClass = usedClassDeclarations.Any(cl => cl != classDeclaration);

                        var sameClassIdentifiersAfterThis = identifierClassMappings
                            .Where(mapping => mapping.ClassDeclaration == classDeclaration)
                            .Where(mapping => !mapping.Identifier.Field.IsConst)
                            .Where(mapping => mapping.Identifier.Field.DeclaringSyntaxReferences.First().Span.Start > variable.SpanStart);
                        var isAnyAfterInSameClass = sameClassIdentifiersAfterThis.Any();

                        if (isAnyInDifferentClass ||
                            isAnyAfterInSameClass)
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, variable.Initializer.GetLocation()));
                        }
                    }
                },
                SyntaxKind.FieldDeclaration);
        }

        private static List<IdentifierClassDeclarationMapping> GetIdentifierClassMappings(List<IdentifierFieldMapping> identifierFieldMappings)
        {
            return identifierFieldMappings
                .Select(i => new IdentifierClassDeclarationMapping
                {
                    Identifier = i,
                    ClassDeclaration = GetClassDeclaration(i.Field)
                })
                .Where(mapping => mapping.ClassDeclaration != null)
                .ToList();
        }

        private static List<IdentifierFieldMapping> GetIdentifierFieldMappings(VariableDeclaratorSyntax variable,
            INamedTypeSymbol containingType, SemanticModel semanticModel)
        {
            return variable.Initializer.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(identifier =>
                {
                    var field = semanticModel.GetSymbolInfo(identifier).Symbol as IFieldSymbol;
                    var enclosingSymbol = semanticModel.GetEnclosingSymbol(identifier.SpanStart);
                    return new IdentifierFieldMapping
                    {
                        Identifier = identifier,
                        Field = field,
                        IsRelevant = field != null &&
                            field.IsStatic &&
                            containingType.Equals(field.ContainingType) &&
                            enclosingSymbol is IFieldSymbol &&
                            enclosingSymbol.ContainingType.Equals(field.ContainingType)
                    };
                })
                .Where(identifier => identifier.IsRelevant)
                .ToList();
        }

        private static ClassDeclarationSyntax GetClassDeclaration(IFieldSymbol field)
        {
            var reference = field.DeclaringSyntaxReferences.FirstOrDefault();
            if (reference == null ||
                reference.SyntaxTree == null)
            {
                return null;
            }
            return reference.GetSyntax().FirstAncestorOrSelf<ClassDeclarationSyntax>();
        }

        private class IdentifierFieldMapping
        {
            public IdentifierNameSyntax Identifier { get; set; }
            public IFieldSymbol Field { get; set; }
            public bool IsRelevant { get; set; }
        }

        private class IdentifierClassDeclarationMapping
        {
            public IdentifierFieldMapping Identifier { get; set; }
            public ClassDeclarationSyntax ClassDeclaration { get; set; }
        }
    }
}
