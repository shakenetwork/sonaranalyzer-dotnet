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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, Severity.Major, Title, IsActivatedByDefault)]
    [Tags(Tag.Unused)]
    public class UnusedPrivateMember : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1144";
        internal const string Title = "Unused private types or members should be removed";
        internal const string Description =
            "Private types or members that are never executed or referenced are dead code: unnecessary, inoperative code " +
            "that should be removed. Cleaning out dead code decreases the size of the maintained codebase, making it easier " +
            "to understand the program and preventing bugs from being introduced.";
        internal const string MessageFormat = "Remove this unused private member.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                DiagnosticSeverity.Info, IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: IdeVisibility.Hidden.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly Accessibility maxAccessibility = Accessibility.Private;

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var namedType = (INamedTypeSymbol)c.Symbol;
                    if (!namedType.IsClassOrStruct() ||
                        namedType.ContainingType != null)
                    {
                        return;
                    }

                    var declarationCollector = new RemovableDeclarationCollector(namedType, c.Compilation);

                    var declaredPrivateSymbols = new HashSet<ISymbol>();
                    var fieldLikeSymbols = new BidirectionalDictionary<ISymbol, SyntaxNode>();

                    CollectRemovableNamedTypes(declarationCollector, declaredPrivateSymbols);
                    CollectRemovableFieldLikeDeclarations(declarationCollector, declaredPrivateSymbols, fieldLikeSymbols);
                    CollectRemovableEventsAndProperties(declarationCollector, declaredPrivateSymbols);
                    CollectRemovableMethods(declarationCollector, declaredPrivateSymbols);

                    if (!declaredPrivateSymbols.Any())
                    {
                        return;
                    }

                    var usedSymbols = new HashSet<ISymbol>();
                    var emptyConstructors = new HashSet<ISymbol>();

                    CollectUsedSymbols(declarationCollector, usedSymbols, declaredPrivateSymbols);
                    CollectUsedSymbolsFromCtorInitializerAndCollectEmptyCtors(declarationCollector,
                        usedSymbols, emptyConstructors);

                    ReportIssues(c, usedSymbols, declaredPrivateSymbols, emptyConstructors, fieldLikeSymbols);
                },
                SymbolKind.NamedType);
        }


        private static void ReportIssues(SymbolAnalysisContext context, HashSet<ISymbol> usedSymbols,
            HashSet<ISymbol> declaredPrivateSymbols, HashSet<ISymbol> emptyConstructors,
            BidirectionalDictionary<ISymbol, SyntaxNode> fieldLikeSymbols)
        {
            var unusedSymbols = declaredPrivateSymbols
                .Except(usedSymbols.Union(emptyConstructors))
                .ToList();

            var alreadyReportedFieldLikeSymbols = new HashSet<ISymbol>();

            var unusedSymbolSyntaxPairs = unusedSymbols
                .SelectMany(unusedSymbol => unusedSymbol.DeclaringSyntaxReferences
                    .Select(r =>
                        new
                        {
                            Syntax = r.GetSyntax(),
                            Symbol = unusedSymbol
                        }));

            foreach (var unused in unusedSymbolSyntaxPairs)
            {
                var location = unused.Syntax.GetLocation();

                var canBeFieldLike = unused.Symbol is IFieldSymbol || unused.Symbol is IEventSymbol;
                if (canBeFieldLike)
                {
                    if (alreadyReportedFieldLikeSymbols.Contains(unused.Symbol))
                    {
                        continue;
                    }

                    var variableDeclaration = GetVariableDeclaration(unused.Syntax);
                    if (variableDeclaration == null)
                    {
                        continue;
                    }

                    var declarations = variableDeclaration.Variables
                        .Select(v => fieldLikeSymbols.GetByB(v))
                        .ToList();

                    if (declarations.All(d => unusedSymbols.Contains(d)))
                    {
                        location = unused.Syntax.Parent.Parent.GetLocation();
                        alreadyReportedFieldLikeSymbols.UnionWith(declarations);
                    }
                }

                context.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule, location), context.Compilation);
            }
        }

        private static void CollectRemovableMethods(RemovableDeclarationCollector declarationCollector,
            HashSet<ISymbol> declaredPrivateSymbols)
        {
            var methodSymbols = declarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes(RemovableDeclarationCollector.IsNodeContainerTypeDeclaration)
                    .Where(node =>
                        node.IsKind(SyntaxKind.MethodDeclaration) ||
                        node.IsKind(SyntaxKind.ConstructorDeclaration))
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }))
                    .Select(node => node.SemanticModel.GetDeclaredSymbol(node.SyntaxNode) as IMethodSymbol)
                    .Where(method => RemovableDeclarationCollector.IsRemovable(method, maxAccessibility));

            declaredPrivateSymbols.UnionWith(methodSymbols);
        }


        private static void CollectRemovableEventsAndProperties(RemovableDeclarationCollector helper,
            HashSet<ISymbol> declaredPrivateSymbols)
        {
            var declarationKinds = ImmutableHashSet.Create(SyntaxKind.EventDeclaration, SyntaxKind.PropertyDeclaration, SyntaxKind.IndexerDeclaration);
            var declarations = helper.GetRemovableDeclarations(declarationKinds, maxAccessibility);
            declaredPrivateSymbols.UnionWith(declarations.Select(d => d.Symbol));
        }

        private static void CollectRemovableFieldLikeDeclarations(RemovableDeclarationCollector declarationCollector,
            HashSet<ISymbol> declaredPrivateSymbols, BidirectionalDictionary<ISymbol, SyntaxNode> fieldLikeSymbols)
        {
            var declarationKinds = ImmutableHashSet.Create(SyntaxKind.FieldDeclaration, SyntaxKind.EventFieldDeclaration);
            var removableFieldsDefinitions = declarationCollector.GetRemovableFieldLikeDeclarations(declarationKinds, maxAccessibility);

            foreach (var fieldsDefinitions in removableFieldsDefinitions)
            {
                declaredPrivateSymbols.Add(fieldsDefinitions.Symbol);
                fieldLikeSymbols.Add(fieldsDefinitions.Symbol, fieldsDefinitions.SyntaxNode);
            }
        }

        private static void CollectRemovableNamedTypes(RemovableDeclarationCollector declarationCollector,
            HashSet<ISymbol> declaredPrivateSymbols)
        {
            var symbols = declarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes(RemovableDeclarationCollector.IsNodeContainerTypeDeclaration)
                    .Where(node =>
                        node.IsKind(SyntaxKind.ClassDeclaration) ||
                        node.IsKind(SyntaxKind.InterfaceDeclaration) ||
                        node.IsKind(SyntaxKind.StructDeclaration) ||
                        node.IsKind(SyntaxKind.DelegateDeclaration))
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }))
                    .Select(node => node.SemanticModel.GetDeclaredSymbol(node.SyntaxNode))
                    .Where(symbol => RemovableDeclarationCollector.IsRemovable(symbol, maxAccessibility));

            declaredPrivateSymbols.UnionWith(symbols);
        }

        private static void CollectUsedSymbolsFromCtorInitializerAndCollectEmptyCtors(
            RemovableDeclarationCollector declarationCollector,
            HashSet<ISymbol> usedSymbols, HashSet<ISymbol> emptyConstructors)
        {
            var ctors = declarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes(RemovableDeclarationCollector.IsNodeStructOrClassDeclaration)
                    .Where(node => node.IsKind(SyntaxKind.ConstructorDeclaration))
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<ConstructorDeclarationSyntax>
                        {
                            SyntaxNode = (ConstructorDeclarationSyntax)node,
                            SemanticModel = container.SemanticModel
                        }));

            foreach (var ctor in ctors)
            {
                if (ctor.SyntaxNode.Body == null ||
                    !ctor.SyntaxNode.Body.Statements.Any())
                {
                    var ctorSymbol = ctor.SemanticModel.GetDeclaredSymbol(ctor.SyntaxNode);
                    if (ctorSymbol != null &&
                        !ctorSymbol.Parameters.Any())
                    {
                        emptyConstructors.Add(ctorSymbol.OriginalDefinition);
                    }
                }

                if (ctor.SyntaxNode.Initializer == null)
                {
                    continue;
                }

                var baseCtor = ctor.SemanticModel.GetSymbolInfo(ctor.SyntaxNode.Initializer).Symbol;
                if (baseCtor != null)
                {
                    usedSymbols.Add(baseCtor);
                }
            }
        }

        private static void CollectUsedSymbols(RemovableDeclarationCollector declarationCollector,
            HashSet<ISymbol> usedSymbols, HashSet<ISymbol> declaredPrivateSymbols)
        {
            var symbolNames = declaredPrivateSymbols.Select(s => s.Name).ToImmutableHashSet();
            var anyRemovableIndexers = declaredPrivateSymbols
                .OfType<IPropertySymbol>()
                .Any(p => p.IsIndexer);
            var anyRemovableCtors = declaredPrivateSymbols
                .OfType<IMethodSymbol>()
                .Any(m => m.MethodKind == MethodKind.Constructor);

            var identifiers = declarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .Where(node =>
                        node.IsKind(SyntaxKind.IdentifierName))
                    .Cast<IdentifierNameSyntax>()
                    .Where(node => symbolNames.Contains(node.Identifier.ValueText))
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }));

            var generic = declarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .Where(node =>
                        node.IsKind(SyntaxKind.GenericName))
                    .Cast<GenericNameSyntax>()
                    .Where(node => symbolNames.Contains(node.Identifier.ValueText))
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }));

            var allNodes = identifiers.Concat(generic);

            if (anyRemovableIndexers)
            {
                var nodes = declarationCollector.TypeDeclarations
                    .SelectMany(container => container.SyntaxNode.DescendantNodes()
                        .Where(node => node.IsKind(SyntaxKind.ElementAccessExpression))
                        .Select(node =>
                            new SyntaxNodeSemanticModelTuple<SyntaxNode>
                            {
                                SyntaxNode = node,
                                SemanticModel = container.SemanticModel
                            }));

                allNodes = allNodes.Concat(nodes);
            }

            if (anyRemovableCtors)
            {
                var nodes = declarationCollector.TypeDeclarations
                    .SelectMany(container => container.SyntaxNode.DescendantNodes()
                        .Where(node => node.IsKind(SyntaxKind.ObjectCreationExpression))
                        .Select(node =>
                            new SyntaxNodeSemanticModelTuple<SyntaxNode>
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

                if (methodSymbol?.MethodKind == MethodKind.ReducedExtension)
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
    }
}