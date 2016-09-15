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

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Design)]
    public class ClassNotInstantiatable : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3453";
        internal const string Title = "Classes should not have only \"private\" constructors";
        internal const string Description =
            "A class with only \"private\" constructors can't be instantiated, thus, it seems to be pointless code.";
        internal const string MessageFormat = "This class can't be instantiated; make {0} \"public\".";
        internal const string Category = SonarAnalyzer.Common.Category.Design;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(
                c => CheckClassWithOnlyUnusedPrivateConstructors(c),
                SymbolKind.NamedType);
        }

        private static void CheckClassWithOnlyUnusedPrivateConstructors(SymbolAnalysisContext context)
        {
            var namedType = context.Symbol as INamedTypeSymbol;
            if (!IsNonStaticClassWithNoAttributes(namedType))
            {
                return;
            }

            var members = namedType.GetMembers();
            var constructors = GetConstructors(members).ToList();

            if (!HasOnlyCandidateConstructors(constructors) ||
                HasOnlyStaticMembers(members.Except(constructors).ToList()))
            {
                return;
            }

            var typeDeclarations = new RemovableDeclarationCollector(namedType, context.Compilation).TypeDeclarations;

            if (!IsAnyConstructorCalled(namedType, typeDeclarations))
            {
                var message = constructors.Count > 1
                    ? "at least one of its constructors"
                    : "its constructor";

                foreach (var classDeclaration in typeDeclarations)
                {
                    context.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule, classDeclaration.SyntaxNode.Identifier.GetLocation(),
                        message));
                }
            }
        }

        private static bool HasOnlyCandidateConstructors(ICollection<IMethodSymbol> constructors)
        {
            return constructors.Any() &&
                !HasNonPrivateConstructor(constructors) &&
                constructors.All(c => !c.GetAttributes().Any());
        }

        private static bool IsNonStaticClassWithNoAttributes(INamedTypeSymbol namedType)
        {
            return namedType.IsClass() &&
                !namedType.IsStatic &&
                !namedType.GetAttributes().Any();
        }

        private static bool IsAnyConstructorCalled(INamedTypeSymbol namedType,
            IEnumerable<SyntaxNodeSemanticModelTuple<BaseTypeDeclarationSyntax>> typeDeclarations)
        {
            return typeDeclarations
                .Select(classDeclaration => new
                {
                    SemanticModel = classDeclaration.SemanticModel,
                    DescendantNodes = classDeclaration.SyntaxNode.DescendantNodes().ToList()
                })
                .Any(descendants =>
                    IsAnyConstructorToCurrentType(descendants.DescendantNodes, namedType, descendants.SemanticModel) ||
                    IsAnyNestedTypeExtendingCurrentType(descendants.DescendantNodes, namedType, descendants.SemanticModel));
        }

        private static bool HasNonPrivateConstructor(IEnumerable<IMethodSymbol> constructors)
        {
            return constructors.Any(method => method.DeclaredAccessibility != Accessibility.Private);
        }

        private static bool IsAnyNestedTypeExtendingCurrentType(IEnumerable<SyntaxNode> descendantNodes, INamedTypeSymbol namedType,
            SemanticModel semanticModel)
        {
            return descendantNodes
                .OfType<ClassDeclarationSyntax>()
                .Select(c => semanticModel.GetDeclaredSymbol(c)?.BaseType)
                .Any(baseType => baseType != null && baseType.OriginalDefinition.DerivesFrom(namedType));
        }

        private static bool IsAnyConstructorToCurrentType(IEnumerable<SyntaxNode> descendantNodes, INamedTypeSymbol namedType,
            SemanticModel semanticModel)
        {
            return descendantNodes
                .OfType<ObjectCreationExpressionSyntax>()
                .Select(ctor => semanticModel.GetSymbolInfo(ctor).Symbol as IMethodSymbol)
                .Where(m => m != null)
                .Any(ctor => object.Equals(ctor.ContainingType?.OriginalDefinition, namedType));
        }

        private static IEnumerable<IMethodSymbol> GetConstructors(IEnumerable<ISymbol> members)
        {
            return members
                .OfType<IMethodSymbol>()
                .Where(method => method.MethodKind == MethodKind.Constructor);
        }

        private static bool HasOnlyStaticMembers(ICollection<ISymbol> members)
        {
            return members.Any() &&
                members.All(member => member.IsStatic);
        }
    }
}
