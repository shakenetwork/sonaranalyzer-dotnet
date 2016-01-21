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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, Severity.Major, Title, IsActivatedByDefault)]
    [Tags(Tag.Unused)]
    public class UnusedPrivateMember : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1144";
        internal const string Title = "Unused private types or members should be removed";
        internal const string Description =
            "Private types or members that are never executed or referenced are dead code: unnecessary, inoperative code " +
            "that should be removed. Cleaning out dead code decreases the size of the maintained codebase, making it easier " +
            "to understand the program and preventing bugs from being introduced.";
        internal const string MessageFormat = "Remove this unused private member.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                DiagnosticSeverity.Info, IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: IdeVisibility.Hidden.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private class SyntaxNodeWithSemanticModel<T> where T : SyntaxNode
        {
            public T SyntaxNode { get; set; }
            public SemanticModel SemanticModel { get; set; }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var namedType = (INamedTypeSymbol)c.Symbol;
                    if (namedType.TypeKind != TypeKind.Class &&
                        namedType.TypeKind != TypeKind.Struct)
                    {
                        return;
                    }

                    if (namedType.ContainingType != null)
                    {
                        return;
                    }

                    var classDeclarations = namedType.DeclaringSyntaxReferences
                        .Select(n => n.GetSyntax())
                        .Select(n =>
                            new SyntaxNodeWithSemanticModel<SyntaxNode>
                            {
                                SyntaxNode = n,
                                SemanticModel = c.Compilation.GetSemanticModel(n.SyntaxTree)
                            })
                        .Where(n => n.SemanticModel != null)
                        .ToList();

                    var declaredPrivateSymbols = new HashSet<ISymbol>();
                    var fieldLikeSymbols = new BidirectionalDictionary<ISymbol, SyntaxNode>();

                    CollectRemovableNamedTypes(classDeclarations, declaredPrivateSymbols);
                    CollectRemovableFieldDeclarations(classDeclarations, declaredPrivateSymbols, fieldLikeSymbols);
                    CollectRemovableEventFieldDeclarations(classDeclarations, declaredPrivateSymbols, fieldLikeSymbols);
                    CollectRemovableEventsAndProperties(classDeclarations, declaredPrivateSymbols);
                    CollectRemovableMethods(classDeclarations, declaredPrivateSymbols);

                    if (!declaredPrivateSymbols.Any())
                    {
                        return;
                    }

                    var usedSymbols = new HashSet<ISymbol>();
                    var emptyConstructors = new HashSet<ISymbol>();

                    var removableSymbolNames = declaredPrivateSymbols.Select(s => s.Name).ToImmutableHashSet();
                    var anyRemovableIndexers = declaredPrivateSymbols
                        .OfType<IPropertySymbol>()
                        .Any(p => p.IsIndexer);
                    var anyRemovableCtors = declaredPrivateSymbols
                        .OfType<IMethodSymbol>()
                        .Any(m => m.MethodKind == MethodKind.Constructor);

                    CollectUsedSymbols(classDeclarations, usedSymbols, removableSymbolNames, anyRemovableIndexers, anyRemovableCtors);
                    CollectUsedSymbolsFromCtorInitializerAndCollectEmptyCtors(classDeclarations,
                        usedSymbols, emptyConstructors);

                    ReportIssues(c, usedSymbols, declaredPrivateSymbols, emptyConstructors, fieldLikeSymbols);
                },
                SymbolKind.NamedType);
        }

        private static void ReportIssues(SymbolAnalysisContext c, HashSet<ISymbol> usedSymbols,
            HashSet<ISymbol> declaredPrivateSymbols, HashSet<ISymbol> emptyConstructors,
            BidirectionalDictionary<ISymbol, SyntaxNode> fieldLikeSymbols)
        {
            var unusedSymbols = declaredPrivateSymbols
                .Except(usedSymbols.Union(emptyConstructors))
                .ToList();

            var alreadyReportedFieldLikeSymbols = new HashSet<ISymbol>();

            foreach (var unusedSymbol in unusedSymbols)
            {
                foreach (var syntaxReference in unusedSymbol.DeclaringSyntaxReferences)
                {
                    var syntax = syntaxReference.GetSyntax();
                    var location = syntax.GetLocation();

                    var canBeFieldLike = unusedSymbol is IFieldSymbol || unusedSymbol is IEventSymbol;
                    if (canBeFieldLike)
                    {
                        if (alreadyReportedFieldLikeSymbols.Contains(unusedSymbol))
                        {
                            continue;
                        }

                        var variableDeclaration = GetVariableDeclaration(syntax);
                        if (variableDeclaration == null)
                        {
                            continue;
                        }

                        var declarations = variableDeclaration.Variables
                            .Select(v => fieldLikeSymbols.GetByB(v))
                            .ToList();

                        if (declarations.All(d => unusedSymbols.Contains(d)))
                        {
                            location = syntax.Parent.Parent.GetLocation();

                            foreach (var declaration in declarations)
                            {
                                alreadyReportedFieldLikeSymbols.Add(declaration);
                            }
                        }
                    }

                    c.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule, location), c.Compilation);
                }
            }
        }

        private static readonly Func<SyntaxNode, bool> IsNodeStructOrClassDeclaration =
            node => node.IsKind(SyntaxKind.ClassDeclaration) ||
                    node.IsKind(SyntaxKind.StructDeclaration);

        private static readonly Func<SyntaxNode, bool> IsNodeContainerTypeDeclaration =
            node => IsNodeStructOrClassDeclaration(node) ||
                    node.IsKind(SyntaxKind.InterfaceDeclaration);

        private static void CollectRemovableMethods(IEnumerable<SyntaxNodeWithSemanticModel<SyntaxNode>> containers,
            HashSet<ISymbol> declaredPrivateSymbols)
        {
            var methodSymbols = containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes(IsNodeContainerTypeDeclaration)
                    .Where(node =>
                        node.IsKind(SyntaxKind.MethodDeclaration) ||
                        node.IsKind(SyntaxKind.ConstructorDeclaration))
                    .Select(node =>
                        new SyntaxNodeWithSemanticModel<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }))
                    .Select(node => node.SemanticModel.GetDeclaredSymbol(node.SyntaxNode) as IMethodSymbol)
                    .Where(IsMethodSymbolQualifyingPrivate);

            declaredPrivateSymbols.UnionWith(methodSymbols);
        }

        private static bool IsMethodSymbolQualifyingPrivate(IMethodSymbol methodSymbol)
        {
            return methodSymbol != null &&
                IsSymbolRemovable(methodSymbol) &&
                new[] { MethodKind.Ordinary, MethodKind.Constructor }.Contains(methodSymbol.MethodKind) &&
                methodSymbol.ContainingType.TypeKind != TypeKind.Interface &&
                !methodSymbol.IsInterfaceImplementationOrMemberOverride() &&
                !IsMainMethod(methodSymbol);
        }

        private static void CollectRemovableEventsAndProperties(IEnumerable<SyntaxNodeWithSemanticModel<SyntaxNode>> containers,
            HashSet<ISymbol> declaredPrivateSymbols)
        {
            var symbols = containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes(IsNodeContainerTypeDeclaration)
                    .Where(node =>
                        node.IsKind(SyntaxKind.EventDeclaration) ||
                        node.IsKind(SyntaxKind.PropertyDeclaration) ||
                        node.IsKind(SyntaxKind.IndexerDeclaration))
                    .Select(node =>
                        new SyntaxNodeWithSemanticModel<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }))
                    .Select(node => node.SemanticModel.GetDeclaredSymbol(node.SyntaxNode))
                    .Where(symbol => symbol != null && IsCandidateSymbolNotInInterfaceAndChangeable(symbol));

            declaredPrivateSymbols.UnionWith(symbols);
        }

        private static void CollectRemovableEventFieldDeclarations(IEnumerable<SyntaxNodeWithSemanticModel<SyntaxNode>> containers,
            HashSet<ISymbol> declaredPrivateSymbols, BidirectionalDictionary<ISymbol, SyntaxNode> fieldLikeSymbols)
        {
            var fields = containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes(IsNodeContainerTypeDeclaration)
                    .Where(node => node.IsKind(SyntaxKind.EventFieldDeclaration))
                    .Select(node =>
                        new SyntaxNodeWithSemanticModel<EventFieldDeclarationSyntax>
                        {
                            SyntaxNode = (EventFieldDeclarationSyntax)node,
                            SemanticModel = container.SemanticModel
                        }));

            foreach (var field in fields)
            {
                var removableFieldsDefinitions = field.SyntaxNode.Declaration.Variables
                        .Select(variable =>
                            new
                            {
                                Variable = variable,
                                EventSymbol = field.SemanticModel.GetDeclaredSymbol(variable) as IEventSymbol
                            })
                        .Where(p => p.EventSymbol != null)
                        .Where(p => IsCandidateSymbolNotInInterfaceAndChangeable(p.EventSymbol));

                foreach (var fieldsDefinitions in removableFieldsDefinitions)
                {
                    declaredPrivateSymbols.Add(fieldsDefinitions.EventSymbol);
                    fieldLikeSymbols.Add(fieldsDefinitions.EventSymbol, fieldsDefinitions.Variable);
                }
            }
        }

        private static void CollectRemovableFieldDeclarations(IEnumerable<SyntaxNodeWithSemanticModel<SyntaxNode>> containers,
            HashSet<ISymbol> declaredPrivateSymbols, BidirectionalDictionary<ISymbol, SyntaxNode> fieldLikeSymbols)
        {
            var fields = containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes(IsNodeStructOrClassDeclaration)
                    .Where(node => node.IsKind(SyntaxKind.FieldDeclaration))
                    .Select(node =>
                        new SyntaxNodeWithSemanticModel<FieldDeclarationSyntax>
                        {
                            SyntaxNode = (FieldDeclarationSyntax)node,
                            SemanticModel = container.SemanticModel
                        }));

            foreach (var field in fields)
            {
                var removableFieldsDefinitions = field.SyntaxNode.Declaration.Variables
                    .Select(variable =>
                        new
                        {
                            Variable = variable,
                            FieldSymbol = field.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol
                        })
                    .Where(p => p.FieldSymbol != null)
                    .Where(p => IsSymbolRemovable(p.FieldSymbol));

                foreach (var fieldsDefinitions in removableFieldsDefinitions)
                {
                    declaredPrivateSymbols.Add(fieldsDefinitions.FieldSymbol);
                    fieldLikeSymbols.Add(fieldsDefinitions.FieldSymbol, fieldsDefinitions.Variable);
                }
            }
        }

        private static void CollectRemovableNamedTypes(IEnumerable<SyntaxNodeWithSemanticModel<SyntaxNode>> containers,
            HashSet<ISymbol> declaredPrivateSymbols)
        {
            var symbols = containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes(IsNodeContainerTypeDeclaration)
                    .Where(node =>
                        node.IsKind(SyntaxKind.ClassDeclaration) ||
                        node.IsKind(SyntaxKind.InterfaceDeclaration) ||
                        node.IsKind(SyntaxKind.StructDeclaration) ||
                        node.IsKind(SyntaxKind.DelegateDeclaration))
                    .Select(node =>
                        new SyntaxNodeWithSemanticModel<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }))
                    .Select(node => node.SemanticModel.GetDeclaredSymbol(node.SyntaxNode))
                    .Where(symbol => symbol != null && IsSymbolRemovable(symbol));

            declaredPrivateSymbols.UnionWith(symbols);
        }

        private static void CollectUsedSymbolsFromCtorInitializerAndCollectEmptyCtors(
            IEnumerable<SyntaxNodeWithSemanticModel<SyntaxNode>> containers,
            HashSet<ISymbol> usedSymbols, HashSet<ISymbol> emptyConstructors)
        {
            var ctors = containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes(IsNodeStructOrClassDeclaration)
                    .Where(node => node.IsKind(SyntaxKind.ConstructorDeclaration))
                    .Select(node =>
                        new SyntaxNodeWithSemanticModel<ConstructorDeclarationSyntax>
                        {
                            SyntaxNode = (ConstructorDeclarationSyntax)node,
                            SemanticModel = container.SemanticModel
                        }));

            foreach (var ctor in ctors)
            {
                if (!ctor.SyntaxNode.Body.Statements.Any())
                {
                    var ctorSymbol = ctor.SemanticModel.GetDeclaredSymbol(ctor.SyntaxNode);
                    if (ctorSymbol != null &&
                        !ctorSymbol.Parameters.Any())
                    {
                        emptyConstructors.Add(ctorSymbol.OriginalDefinition);
                    }
                }

                if (ctor.SyntaxNode.Initializer != null)
                {
                    var baseCtor = ctor.SemanticModel.GetSymbolInfo(ctor.SyntaxNode.Initializer).Symbol;
                    if (baseCtor != null)
                    {
                        usedSymbols.Add(baseCtor);
                    }
                }
            }
        }

        private static void CollectUsedSymbols(IList<SyntaxNodeWithSemanticModel<SyntaxNode>> containers,
            HashSet<ISymbol> usedSymbols, ImmutableHashSet<string> symbolNames,
            bool anyRemovableIndexers, bool anyRemovableCtors)
        {
            var identifiers = containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .Where(node =>
                        node.IsKind(SyntaxKind.IdentifierName))
                    .Select(node => (IdentifierNameSyntax)node)
                    .Where(node => symbolNames.Contains(node.Identifier.ValueText))
                    .Select(node =>
                        new SyntaxNodeWithSemanticModel<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }));

            var generic = containers
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .Where(node =>
                        node.IsKind(SyntaxKind.GenericName))
                    .Select(node => (GenericNameSyntax)node)
                    .Where(node => symbolNames.Contains(node.Identifier.ValueText))
                    .Select(node =>
                        new SyntaxNodeWithSemanticModel<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }));

            var allNodes = identifiers.Concat(generic);

            if (anyRemovableIndexers)
            {
                var nodes = containers
                    .SelectMany(container => container.SyntaxNode.DescendantNodes()
                        .Where(node => node.IsKind(SyntaxKind.ElementAccessExpression))
                        .Select(node =>
                            new SyntaxNodeWithSemanticModel<SyntaxNode>
                            {
                                SyntaxNode = node,
                                SemanticModel = container.SemanticModel
                            }));

                allNodes = allNodes.Concat(nodes);
            }

            if (anyRemovableCtors)
            {
                var nodes = containers
                    .SelectMany(container => container.SyntaxNode.DescendantNodes()
                        .Where(node => node.IsKind(SyntaxKind.ObjectCreationExpression))
                        .Select(node =>
                            new SyntaxNodeWithSemanticModel<SyntaxNode>
                            {
                                SyntaxNode = node,
                                SemanticModel = container.SemanticModel
                            }));

                allNodes = allNodes.Concat(nodes);
            }

            foreach (var node in allNodes)
            {
                var symbol = node.SemanticModel.GetSymbolInfo(node.SyntaxNode).Symbol;
                var methodSymbol = symbol as IMethodSymbol;

                if (methodSymbol != null &&
                    methodSymbol.MethodKind == MethodKind.ReducedExtension)
                {
                    symbol = methodSymbol.ReducedFrom;
                }

                if (symbol != null)
                {
                    usedSymbols.Add(symbol.OriginalDefinition);
                }
            }
        }

        private static VariableDeclarationSyntax GetVariableDeclaration(SyntaxNode syntax)
        {
            var fieldDeclaration = syntax.Parent.Parent as FieldDeclarationSyntax;
            if (fieldDeclaration != null)
            {
                return fieldDeclaration.Declaration;
            }

            var eventFieldDeclaration = syntax.Parent.Parent as EventFieldDeclarationSyntax;
            return eventFieldDeclaration?.Declaration;
        }

        private static bool IsCandidateSymbolNotInInterfaceAndChangeable(ISymbol symbol)
        {
            return IsSymbolRemovable(symbol) &&
                symbol.ContainingType.TypeKind != TypeKind.Interface &&
                !symbol.IsInterfaceImplementationOrMemberOverride();
        }

        private static bool IsSymbolRemovable(ISymbol symbol)
        {
            return EffectiveAccessibility(symbol) == Accessibility.Private &&
                !symbol.IsImplicitlyDeclared &&
                !symbol.IsAbstract &&
                !symbol.IsVirtual &&
                !symbol.GetAttributes().Any();
        }

        private static bool IsMainMethod(IMethodSymbol methodSymbol)
        {
            return methodSymbol.IsStatic && methodSymbol.Name == "Main";
        }

        private static Accessibility EffectiveAccessibility(ISymbol symbol)
        {
            var result = symbol.DeclaredAccessibility;
            var currentSymbol = symbol;

            while (currentSymbol != null)
            {
                if (currentSymbol.DeclaredAccessibility == Accessibility.Private)
                {
                    return Accessibility.Private;
                }
                if (currentSymbol.DeclaredAccessibility == Accessibility.Internal)
                {
                    result = currentSymbol.DeclaredAccessibility;
                }
                currentSymbol = currentSymbol.ContainingType;
            }
            return result;
        }
    }
}
