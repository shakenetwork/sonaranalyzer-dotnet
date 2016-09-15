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

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Unused)]
    public class GenericTypeParameterUnused : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2326";
        internal const string Title = "Unused type parameters should be removed";
        internal const string Description =
            "Type parameters that aren't used are dead code, which can only distract and possibly confuse " +
            "developers during maintenance. Therefore, unused type parameters should be removed.";
        internal const string MessageFormat = "\"{0}\" is not used in the {1}.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
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

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterCompilationStartAction(analysisContext =>
            {
                analysisContext.RegisterSyntaxNodeAction(
                    c =>
                    {
                        var methodDeclaration = c.Node as MethodDeclarationSyntax;
                        var classDeclaration = c.Node as ClassDeclarationSyntax;

                        if (methodDeclaration != null &&
                            !IsMethodCandidate(methodDeclaration, c.SemanticModel))
                        {
                            return;
                        }

                        var declarationSymbol = c.SemanticModel.GetDeclaredSymbol(c.Node);
                        if (declarationSymbol == null)
                        {
                            return;
                        }

                        var helper = GetTypeParameterHelper(methodDeclaration, classDeclaration);
                        if (helper.TypeParameterList == null || helper.TypeParameterList.Parameters.Count == 0)
                        {
                            return;
                        }

                        var declarations = declarationSymbol.DeclaringSyntaxReferences
                            .Select(reference => reference.GetSyntax());

                        var usedTypeParameters = GetUsedTypeParameters(declarations, c, analysisContext.Compilation);

                        foreach (var typeParameter in helper.TypeParameterList.Parameters
                            .Select(typeParameter => typeParameter.Identifier.Text)
                            .Where(typeParameter => !usedTypeParameters.Contains(typeParameter)))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule,
                                helper.TypeParameterList.Parameters.First(tp => tp.Identifier.Text == typeParameter)
                                    .GetLocation(),
                                typeParameter, helper.ContainerSyntaxTypeName));
                        }
                    },
                    SyntaxKind.MethodDeclaration,
                    SyntaxKind.ClassDeclaration);
            });
        }

        private static TypeParameterHelper GetTypeParameterHelper(MethodDeclarationSyntax methodDeclaration, ClassDeclarationSyntax classDeclaration)
        {
            return classDeclaration == null
                ? new TypeParameterHelper
                {
                    TypeParameterList = methodDeclaration.TypeParameterList,
                    ContainerSyntaxTypeName = "method"
                }
                : new TypeParameterHelper
                {
                    TypeParameterList = classDeclaration.TypeParameterList,
                    ContainerSyntaxTypeName = "class"
                };
        }

        private class TypeParameterHelper
        {
            public TypeParameterListSyntax TypeParameterList { get; set; }
            public string ContainerSyntaxTypeName { get; set; }
        }

        private static bool IsMethodCandidate(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel)
        {
            var syntaxValid =
                !methodDeclaration.Modifiers.Any(modifier => MethodModifiersToSkip.Contains(modifier.Kind())) &&
                methodDeclaration.ExplicitInterfaceSpecifier == null &&
                methodDeclaration.Body != null;

            if (!syntaxValid)
            {
                return false;
            }

            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);

            return methodSymbol != null &&
                methodSymbol.IsChangeable();
        }

        private static List<string> GetUsedTypeParameters(IEnumerable<SyntaxNode> declarations,
            SyntaxNodeAnalysisContext localContext,
            Compilation compilation)
        {
            return declarations
                .SelectMany(declaration => declaration.DescendantNodes())
                .OfType<IdentifierNameSyntax>()
                .Where(identifier => !(identifier.Parent is TypeParameterConstraintClauseSyntax))
                .Select(identifier =>
                {
                    var semanticModelOfThisTree = identifier.SyntaxTree == localContext.Node.SyntaxTree
                        ? localContext.SemanticModel
                        : compilation.GetSemanticModel(identifier.SyntaxTree);

                    return semanticModelOfThisTree?.GetSymbolInfo(identifier).Symbol;
                })
                .Where(symbol => symbol != null && symbol.Kind == SymbolKind.TypeParameter)
                .Select(symbol => symbol.Name)
                .ToList();
        }

        private static readonly SyntaxKind[] MethodModifiersToSkip =
        {
            SyntaxKind.AbstractKeyword,
            SyntaxKind.VirtualKeyword,
            SyntaxKind.OverrideKeyword
        };
    }
}
