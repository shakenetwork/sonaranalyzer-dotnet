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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    [Tags(Tag.Clumsy, Tag.Finding)]
    public class RedundancyInConstructorDestructorDeclaration : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3253";
        internal const string Title = "Constructor and destructor declarations should not be redundant";
        internal const string Description =
            "Since the compiler will automatically invoke the base type's no-argument constructor, there's no need to specify its " +
            "invocation explicitly. Also, when only a single \"public\" parameterless constructor is defined in a class, then that " +
            "constructor can be removed because the compiler would generate it automatically. Similarly, empty \"static\" constructors " +
            "and empty destructors are also wasted keystrokes.";
        internal const string MessageFormat = "Remove this redundant {0}.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), true,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckConstructorDeclaration(c),
                SyntaxKind.ConstructorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckDestructorDeclaration(c),
                SyntaxKind.DestructorDeclaration);
        }

        private static void CheckDestructorDeclaration(SyntaxNodeAnalysisContext context)
        {
            var destructorDeclaration = (DestructorDeclarationSyntax)context.Node;

            if (IsBodyEmpty(destructorDeclaration.Body))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, destructorDeclaration.GetLocation(), "destructor"));
            }
        }

        private static void CheckConstructorDeclaration(SyntaxNodeAnalysisContext context)
        {
            var constructorDeclaration = (ConstructorDeclarationSyntax)context.Node;

            if (IsConstructorRedundant(constructorDeclaration, context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, constructorDeclaration.GetLocation(), "constructor"));
                return;
            }

            var initializer = constructorDeclaration.Initializer;
            if (initializer != null &&
                IsInitializerRedundant(initializer))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, initializer.GetLocation(), "\"base()\" call"));
            }
        }

        private static bool IsInitializerRedundant(ConstructorInitializerSyntax initializer)
        {
            return initializer.IsKind(SyntaxKind.BaseConstructorInitializer) &&
                initializer.ArgumentList != null &&
                !initializer.ArgumentList.Arguments.Any();
        }

        private static bool IsConstructorRedundant(ConstructorDeclarationSyntax constructorDeclaration, SemanticModel semanticModel)
        {
            return IsConstructorParameterless(constructorDeclaration) &&
                IsBodyEmpty(constructorDeclaration.Body) &&
                (IsSinglePublicConstructor(constructorDeclaration, semanticModel) ||
                constructorDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)));
        }

        private static bool IsSinglePublicConstructor(ConstructorDeclarationSyntax constructorDeclaration, SemanticModel semanticModel)
        {
            return constructorDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)) &&
                IsInitializerEmptyOrRedundant(constructorDeclaration.Initializer) &&
                TypeHasExactlyOneConstructor(constructorDeclaration, semanticModel);
        }

        private static bool IsInitializerEmptyOrRedundant(ConstructorInitializerSyntax initializer)
        {
            if (initializer == null)
            {
                return true;
            }

            return initializer.ArgumentList != null &&
                !initializer.ArgumentList.Arguments.Any() &&
                initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.BaseKeyword);
        }

        private static bool TypeHasExactlyOneConstructor(ConstructorDeclarationSyntax constructorDeclaration, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(constructorDeclaration);
            return symbol != null &&
                symbol.ContainingType.GetMembers().OfType<IMethodSymbol>().Count(m => m.MethodKind == MethodKind.Constructor) == 1;
        }

        private static bool IsBodyEmpty(BlockSyntax block)
        {
            return block != null && !block.Statements.Any();
        }

        private static bool IsConstructorParameterless(ConstructorDeclarationSyntax constructorDeclaration)
        {
            return constructorDeclaration.ParameterList != null &&
                !constructorDeclaration.ParameterList.Parameters.Any();
        }
    }
}
