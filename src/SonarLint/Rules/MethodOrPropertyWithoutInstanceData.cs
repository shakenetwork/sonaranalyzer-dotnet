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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("performance")]
    public class MethodOrPropertyWithoutInstanceData : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2325";
        internal const string Title = "Methods and properties that don't access instance data should be static";
        internal const string Description =
            "Class methods and properties that don't access instance data can and should be " +
            "\"static\" to prevent any misunderstanding about the contract of the method.";
        internal const string MessageFormat = "Make \"{0}\" a \"static\" {1}.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.RegisterCompilationStartAction(context =>
            {
                var accessorsCouldBeStatic = ImmutableHashSet<AccessorDeclarationSyntax>.Empty;
                var propertyAccessorsByProperty = ImmutableDictionary<PropertyDeclarationSyntax, ImmutableHashSet<AccessorDeclarationSyntax>>.Empty;

                context.RegisterSyntaxNodeAction(
                    c =>
                    {
                        var property = (PropertyDeclarationSyntax) c.Node;

                        if (property.AccessorList != null &&
                            property.AccessorList.Accessors.Any(accessor => accessor.Body != null))
                        {
                            propertyAccessorsByProperty = propertyAccessorsByProperty.SetItem(property,
                                property.AccessorList.Accessors.ToImmutableHashSet());
                        }
                    }, SyntaxKind.PropertyDeclaration);

                context.RegisterCodeBlockStartAction<SyntaxKind>(
                    cbc =>
                    {
                        var methodDeclaration = cbc.CodeBlock as MethodDeclarationSyntax;
                        var propertyAccessorDeclaration = cbc.CodeBlock as AccessorDeclarationSyntax;
                        var reportShouldBeStatic = true;

                        if (methodDeclaration == null && propertyAccessorDeclaration == null)
                        {
                            return;
                        }

                        var methodOrPropertySymbol = cbc.OwningSymbol;
                        if (methodOrPropertySymbol == null ||
                            HasAllowedModifier(methodOrPropertySymbol) ||
                            IsInterfaceImplementation(methodOrPropertySymbol))
                        {
                            return;
                        }

                        var inheritanceChain = GetInheritanceChain(methodOrPropertySymbol.ContainingType).ToImmutableHashSet();
                        
                        cbc.RegisterSyntaxNodeAction(
                            c =>
                            {
                                var identifierSymbol = c.SemanticModel.GetSymbolInfo(c.Node);
                                if (identifierSymbol.Symbol == null)
                                {
                                    reportShouldBeStatic = false;
                                    return;
                                }

                                if (PossibleMemberSymbolKinds.Contains(identifierSymbol.Symbol.Kind) &&
                                    !identifierSymbol.Symbol.IsStatic &&
                                    inheritanceChain.Contains(identifierSymbol.Symbol.ContainingType))
                                {
                                    reportShouldBeStatic = false;
                                }
                            },
                            SyntaxKind.IdentifierName, SyntaxKind.GenericName);

                        cbc.RegisterSyntaxNodeAction(
                            c =>
                            {
                                reportShouldBeStatic = false;
                            },
                            SyntaxKind.ThisExpression);
                        
                        cbc.RegisterCodeBlockEndAction(
                            c =>
                            {
                                if (!reportShouldBeStatic)
                                {
                                    return;
                                }

                                if (methodDeclaration != null)
                                {
                                    c.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(),
                                        methodDeclaration.Identifier.ValueText, "method"));
                                }
                                else
                                {
                                    accessorsCouldBeStatic = accessorsCouldBeStatic.Add(propertyAccessorDeclaration);
                                }
                            });
                    });

                context.RegisterCompilationEndAction(
                    c =>
                    {
                        foreach (var property in propertyAccessorsByProperty)
                        {
                            var accessors = property.Value;
                            if (accessors.All(accessor => accessorsCouldBeStatic.Contains(accessor)))
                            {
                                c.ReportDiagnostic(Diagnostic.Create(Rule, property.Key.Identifier.GetLocation(),
                                    property.Key.Identifier.ValueText, "property"));
                            }
                        }
                    });
            });
        }

        private static List<ITypeSymbol> GetInheritanceChain(INamedTypeSymbol type)
        {
            var inheritanceChain = new List<ITypeSymbol>();
            var currentType = type;
            while (currentType != null)
            {
                inheritanceChain.Add(currentType);
                currentType = currentType.BaseType;
            }
            return inheritanceChain;
        }

        private static bool HasAllowedModifier(ISymbol symbol)
        {
            return symbol.IsStatic ||
                   symbol.IsVirtual ||
                   symbol.IsOverride ||
                   symbol.IsAbstract;
        }

        private static bool IsInterfaceImplementation(ISymbol symbol)
        {
            var containingType = symbol.ContainingType;

            return containingType.AllInterfaces
                .SelectMany(interf => interf.GetMembers().OfType<IMethodSymbol>())
                .Any(interfaceMember => symbol.Equals(containingType.FindImplementationForInterfaceMember(interfaceMember)));
        }

        private static readonly SymbolKind[] PossibleMemberSymbolKinds =
        {
            SymbolKind.Method,
            SymbolKind.Field,
            SymbolKind.Property,
            SymbolKind.Event
        };
    }
}
