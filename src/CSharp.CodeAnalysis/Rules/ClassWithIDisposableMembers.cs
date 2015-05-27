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
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug", "cwe", "denial-of-service", "security")]
    public class ClassWithIDisposableMembers : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2931";
        internal const string Description = "Classes with \"IDisposable\" members should implement \"IDisposable\"";
        internal const string MessageFormat = "Implement \"IDisposable\" in this class and use the \"Dispose\" method to call \"Dispose\" on {0}.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2931");
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly Accessibility[] Accessibilities = { Accessibility.Protected, Accessibility.Private };

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(analysisContext =>
            {
                var fieldsByNamedType = new Dictionary<INamedTypeSymbol, ImmutableHashSet<IFieldSymbol>>();
                var fieldsAssigned = ImmutableHashSet<IFieldSymbol>.Empty;

                analysisContext.RegisterSymbolAction(c =>
                {
                    var namedTypeSymbol = (INamedTypeSymbol)c.Symbol;
                    if (namedTypeSymbol.TypeKind != TypeKind.Class ||
                        ImplementsIDisposable(namedTypeSymbol))
                    {
                        return;
                    }

                    var disposableFields = namedTypeSymbol.GetMembers()
                        .OfType<IFieldSymbol>()
                        .Where(FieldIsRelevant)
                        .ToImmutableHashSet();

                    if (!fieldsByNamedType.ContainsKey(namedTypeSymbol))
                    {
                        fieldsByNamedType.Add(namedTypeSymbol, disposableFields);
                    }
                    else
                    {
                        fieldsByNamedType[namedTypeSymbol] = fieldsByNamedType[namedTypeSymbol].Union(disposableFields);
                    }
                }, SymbolKind.NamedType);
                

                analysisContext.RegisterSyntaxNodeAction(c =>
                {
                    var assignment = (AssignmentExpressionSyntax)c.Node;
                    var expression = assignment.Right;
                    var fieldSymbol = c.SemanticModel.GetSymbolInfo(assignment.Left).Symbol as IFieldSymbol;

                    fieldsAssigned = AddFieldIfNeeded(fieldSymbol, expression, fieldsAssigned);
                }, SyntaxKind.SimpleAssignmentExpression);

                analysisContext.RegisterSyntaxNodeAction(c =>
                {
                    var field = (FieldDeclarationSyntax)c.Node;

                    foreach (var variableDeclaratorSyntax in field.Declaration.Variables
                        .Where(declaratorSyntax => declaratorSyntax.Initializer != null))
                    {
                        var fieldSymbol = c.SemanticModel.GetDeclaredSymbol(variableDeclaratorSyntax) as IFieldSymbol;

                        fieldsAssigned = AddFieldIfNeeded(fieldSymbol, variableDeclaratorSyntax.Initializer.Value,
                            fieldsAssigned);
                    }

                }, SyntaxKind.FieldDeclaration);

                analysisContext.RegisterCompilationEndAction(c =>
                {
                    foreach (var kv in fieldsByNamedType)
                    {
                        foreach (var classSyntax in kv.Key.DeclaringSyntaxReferences
                            .Select(declaringSyntaxReference => declaringSyntaxReference.GetSyntax())
                            .OfType<ClassDeclarationSyntax>())
                        {
                            var assignedFields = kv.Value.Intersect(fieldsAssigned).ToList();

                            if (!assignedFields.Any())
                            {
                                continue;
                            }
                            var variableNames = string.Join(", ",
                                assignedFields.Select(symbol => string.Format("\"{0}\"", symbol.Name)));

                            c.ReportDiagnostic(Diagnostic.Create(Rule, classSyntax.Identifier.GetLocation(),
                                variableNames));
                        }
                    }
                });
            });
        }

        private static ImmutableHashSet<IFieldSymbol> AddFieldIfNeeded(IFieldSymbol fieldSymbol, ExpressionSyntax expression, 
            ImmutableHashSet<IFieldSymbol> fieldsAssigned)
        {
            var objectCreation = expression as ObjectCreationExpressionSyntax;
            if (objectCreation == null ||
                !FieldIsRelevant(fieldSymbol))
            {
                return fieldsAssigned;
            }

            return fieldsAssigned.Add(fieldSymbol);
        }

        internal static bool FieldIsRelevant(IFieldSymbol fieldSymbol)
        {
            return fieldSymbol != null &&
                   !fieldSymbol.IsStatic &&
                   Accessibilities.Contains(fieldSymbol.DeclaredAccessibility) &&
                   FieldImplementsIDisposable(fieldSymbol);
        }

        internal static bool FieldImplementsIDisposable(IFieldSymbol symbol)
        {
            var namedType = symbol.Type as INamedTypeSymbol;
            return namedType != null && ImplementsIDisposable(namedType);
        }

        private static bool ImplementsIDisposable(INamedTypeSymbol namedTypeSymbol)
        {
            return namedTypeSymbol.AllInterfaces.Any(symbol => symbol.SpecialType == SpecialType.System_IDisposable);
        }
    }
}
