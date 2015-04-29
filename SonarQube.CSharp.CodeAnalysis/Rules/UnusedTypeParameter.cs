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
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("unused")]
    public class UnusedTypeParameter : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2326";
        internal const string Description = "Unused type parameters should be removed";
        internal const string MessageFormat = "\"{0}\" is not used in the {1}.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3A2326");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }
        
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var methodDeclaration = c.Node as MethodDeclarationSyntax;
                    var classDeclaration = c.Node as ClassDeclarationSyntax;

                    if (methodDeclaration == null && classDeclaration == null)
                    {
                        return;
                    }

                    if (methodDeclaration != null &&
                        (methodDeclaration.Modifiers.Any(modifier => ModifiersToSkip.Contains(modifier.Kind())) ||
                         methodDeclaration.Body == null))
                    {
                        return;
                    }

                    var declarationSymbol = c.SemanticModel.GetDeclaredSymbol(c.Node);

                    if (declarationSymbol == null)
                    {
                        return;
                    }
                    
                    TypeParameterListSyntax typeParameterList;
                    string typeOfContainer;
                    if (classDeclaration == null)
                    {
                        typeParameterList = methodDeclaration.TypeParameterList;
                        typeOfContainer = "method";
                    }
                    else
                    {
                        typeParameterList = classDeclaration.TypeParameterList;
                        typeOfContainer = "class";
                    }
                    
                    if (typeParameterList == null || typeParameterList.Parameters.Count == 0)
                    {
                        return;
                    }

                    var typeParameters = typeParameterList.Parameters
                        .Select(typeParameter => typeParameter.Identifier.Text)
                        .ToList();

                    var declarations = declarationSymbol.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax());

                    var usedTypeParameters = declarations.SelectMany(declaration => declaration.DescendantNodes())
                        .OfType<IdentifierNameSyntax>()
                        .Select(identifier => c.SemanticModel.GetSymbolInfo(identifier).Symbol)
                        .Where(symbol => symbol != null && symbol.Kind == SymbolKind.TypeParameter)
                        .Select(symbol => symbol.Name)
                        .ToList();

                    foreach (var typeParameter in typeParameters.Where(typeParameter => !usedTypeParameters.Contains(typeParameter)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule,
                            typeParameterList.Parameters.First(tp => tp.Identifier.Text == typeParameter)
                                .GetLocation(),
                            typeParameter, typeOfContainer));
                    }
                },
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ClassDeclaration);
        }

        public static readonly SyntaxKind[] ModifiersToSkip =
        {
            SyntaxKind.AbstractKeyword,
            SyntaxKind.VirtualKeyword,
            SyntaxKind.OverrideKeyword
        };
    }
}
