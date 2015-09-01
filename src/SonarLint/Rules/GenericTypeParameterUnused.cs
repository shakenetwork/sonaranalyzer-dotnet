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
    [Tags(Tag.Unused)]
    public class GenericTypeParameterUnused : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2326";
        internal const string Title = "Unused type parameters should be removed";
        internal const string Description =
            "Type parameters that aren't used are dead code, which can only distract and possibly confuse " +
            "developers during maintenance. Therefore, unused type parameters should be removed.";
        internal const string MessageFormat = "\"{0}\" is not used in the {1}.";
        internal const string Category = "SonarLint";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(analysisContext =>
            {
                analysisContext.RegisterSyntaxNodeAction(
                    c =>
                    {
                        var methodDeclaration = c.Node as MethodDeclarationSyntax;
                        var classDeclaration = c.Node as ClassDeclarationSyntax;

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

                        var declarations = declarationSymbol.DeclaringSyntaxReferences
                            .Select(reference => reference.GetSyntax());

                        var usedTypeParameters = GetUsedTypeParameters(declarations, c, analysisContext.Compilation);

                        foreach (var typeParameter in
                                typeParameters.Where(typeParameter => !usedTypeParameters.Contains(typeParameter)))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule,
                                typeParameterList.Parameters.First(tp => tp.Identifier.Text == typeParameter)
                                    .GetLocation(),
                                typeParameter, typeOfContainer));
                        }
                    },
                    SyntaxKind.MethodDeclaration,
                    SyntaxKind.ClassDeclaration);
            });
        }

        private static List<string> GetUsedTypeParameters(IEnumerable<SyntaxNode> declarations,
            SyntaxNodeAnalysisContext localContext,
            Compilation compilation)
        {
            return declarations
                .SelectMany(declaration => declaration.DescendantNodes())
                .OfType<IdentifierNameSyntax>()
                .Select(identifier =>
                {
                    var semanticModelOfThisTree = identifier.SyntaxTree == localContext.Node.SyntaxTree
                        ? localContext.SemanticModel
                        : compilation.GetSemanticModel(identifier.SyntaxTree);

                    return semanticModelOfThisTree == null
                        ? null
                        : semanticModelOfThisTree.GetSymbolInfo(identifier).Symbol;
                })
                .Where(symbol => symbol != null && symbol.Kind == SymbolKind.TypeParameter)
                .Select(symbol => symbol.Name)
                .ToList();
        }

        public static readonly SyntaxKind[] ModifiersToSkip =
        {
            SyntaxKind.AbstractKeyword,
            SyntaxKind.VirtualKeyword,
            SyntaxKind.OverrideKeyword
        };
    }
}
