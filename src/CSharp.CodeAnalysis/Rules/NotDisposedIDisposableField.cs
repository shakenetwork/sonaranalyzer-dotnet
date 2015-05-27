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
    //TODO merge this class with the other IDisposable rule
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug", "cwe", "denial-of-service", "security")]
    public class NotDisposedIDisposableField : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2930";
        internal const string Description = "\"IDisposable\" members should be disposed";
        internal const string MessageFormat = "\"Dispose\" of \"{0}\".";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical; 
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2930");

        private static readonly Accessibility[] accessibilities = { Accessibility.Protected, Accessibility.Private };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(analysisContext =>
            {
                var disposableFields = ImmutableHashSet<IFieldSymbol>.Empty;
                var fieldsAssigned = ImmutableHashSet<IFieldSymbol>.Empty;
                var fieldsDisposed = ImmutableHashSet<IFieldSymbol>.Empty;

                analysisContext.RegisterSyntaxNodeAction(c =>
                {
                    var field = (FieldDeclarationSyntax)c.Node;

                    foreach (var variableDeclaratorSyntax in field.Declaration.Variables)
                    {
                        var fieldSymbol = c.SemanticModel.GetDeclaredSymbol(variableDeclaratorSyntax) as IFieldSymbol;

                        if (!FieldIsRelevant(fieldSymbol))
                        {
                            continue;
                        }

                        disposableFields = disposableFields.Add(fieldSymbol);

                        var objectCreation = variableDeclaratorSyntax.Initializer.Value as ObjectCreationExpressionSyntax;
                        if (objectCreation == null)
                        {
                            return;
                        }

                        fieldsAssigned = fieldsAssigned.Add(fieldSymbol);
                    }
                }, SyntaxKind.FieldDeclaration);
                
                analysisContext.RegisterSyntaxNodeAction(c =>
                {
                    var assignment = (AssignmentExpressionSyntax) c.Node;

                    var objectCreation = assignment.Right as ObjectCreationExpressionSyntax;
                    if (objectCreation == null)
                    {
                        return;
                    }

                    var fieldSymbol = c.SemanticModel.GetSymbolInfo(assignment.Left).Symbol as IFieldSymbol;
                    if (!FieldIsRelevant(fieldSymbol))
                    {
                        return;
                    }

                    fieldsAssigned = fieldsAssigned.Add(fieldSymbol);

                }, SyntaxKind.SimpleAssignmentExpression);
                
                analysisContext.RegisterSyntaxNodeAction(c =>
                {
                    var invocation = (InvocationExpressionSyntax) c.Node;
                    var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;

                    if (memberAccess == null)
                    {
                        return;
                    }

                    var fieldSymbol = c.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol as IFieldSymbol;

                    if (!FieldIsRelevant(fieldSymbol))
                    {
                        return;
                    }

                    var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (methodSymbol == null)
                    {
                        return;
                    }

                    var disposeMethod = (IMethodSymbol)analysisContext.Compilation.GetSpecialType(SpecialType.System_IDisposable).GetMembers("Dispose").Single();

                    if (methodSymbol.Equals(
                            methodSymbol.ContainingType.FindImplementationForInterfaceMember(disposeMethod)))
                    {
                        fieldsDisposed = fieldsDisposed.Add(fieldSymbol);
                    }

                }, SyntaxKind.InvocationExpression);

                analysisContext.RegisterCompilationEndAction(c =>
                {
                    var internallyInitializedFields = disposableFields.Intersect(fieldsAssigned);
                    var nonDisposedFields = internallyInitializedFields.Except(fieldsDisposed);

                    foreach (var nonDisposedField in nonDisposedFields)
                    {
                        var declarationReference = nonDisposedField.DeclaringSyntaxReferences.FirstOrDefault();
                        if (declarationReference == null)
                        {
                            continue;
                        }
                        var fieldSyntax = declarationReference.GetSyntax() as VariableDeclaratorSyntax;
                        if (fieldSyntax == null)
                        {
                            continue;
                        }

                        c.ReportDiagnostic(Diagnostic.Create(Rule, fieldSyntax.Identifier.GetLocation(), fieldSyntax.Identifier.ValueText));
                    }
                });
            });
        }

        private static bool FieldIsRelevant(IFieldSymbol fieldSymbol)
        {
            return fieldSymbol != null &&
                   !fieldSymbol.IsStatic &&
                   accessibilities.Contains(fieldSymbol.DeclaredAccessibility) &&
                   FieldImplementsIDisposable(fieldSymbol);
        }

        private static bool FieldImplementsIDisposable(IFieldSymbol symbol)
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
