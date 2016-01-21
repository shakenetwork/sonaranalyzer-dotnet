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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Unused)]
    public class RedundantModifier : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2333";
        internal const string Title = "Redundant modifiers should be removed";
        internal const string Description =
            "Unnecessary keywords simply clutter the code and should be removed.";
        internal const string MessageFormat = "\"{0}\" is {1} in this context.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = false;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        //the severity is set to warning, because otherwise the issue doesn't show up in the IDE
        //this is due to a bug in Roslyn (https://github.com/dotnet/roslyn/issues/4068)
        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckMethodOrProperty(c),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.PropertyDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckTypeDeclaration(c),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.StructDeclaration);

            context.RegisterCompilationStartAction(
                analysisContext =>
                {
                    var classWithBaseType = ImmutableDictionary<INamedTypeSymbol, INamedTypeSymbol>.Empty;

                    analysisContext.RegisterSymbolAction(
                        c =>
                        {
                            var classSymbol = c.Symbol as INamedTypeSymbol;
                            if (classSymbol == null ||
                                classSymbol.TypeKind != TypeKind.Class)
                            {
                                return;
                            }

                            if (!classWithBaseType.ContainsKey(classSymbol))
                            {
                                classWithBaseType = classWithBaseType.Add(classSymbol, classSymbol.BaseType);
                            }
                        },
                        SymbolKind.NamedType);

                    analysisContext.RegisterCompilationEndAction(
                        c =>
                        {
                            foreach (var analyzedClass in classWithBaseType.Keys)
                            {
                                if (DeniedDeclaredAccessibility.Contains(analyzedClass.DeclaredAccessibility) ||
                                    analyzedClass.IsSealed ||
                                    HasDerivedClass(analyzedClass, classWithBaseType))
                                {
                                    continue;
                                }

                                CheckMembers(c, analyzedClass);
                            }
                        });
                });
        }

        private static void CheckMembers(CompilationAnalysisContext c, INamedTypeSymbol analyzedClass)
        {
            foreach (var member in analyzedClass.GetMembers().Where(member => member.IsVirtual))
            {
                var syntax = member.DeclaringSyntaxReferences.First().GetSyntax();

                var methodDeclaration = syntax as MethodDeclarationSyntax;
                if (methodDeclaration != null)
                {
                    var keyword = methodDeclaration.Modifiers.First(m => m.IsKind(SyntaxKind.VirtualKeyword));
                    c.ReportDiagnosticIfNonGenerated(
                        Diagnostic.Create(Rule, keyword.GetLocation(), "virtual", "gratuitous"),
                        c.Compilation);
                    continue;
                }

                var propertyDeclaration = syntax as PropertyDeclarationSyntax;
                if (propertyDeclaration != null)
                {
                    var keyword = propertyDeclaration.Modifiers.First(m => m.IsKind(SyntaxKind.VirtualKeyword));
                    c.ReportDiagnosticIfNonGenerated(
                        Diagnostic.Create(Rule, keyword.GetLocation(), "virtual", "gratuitous"),
                        c.Compilation);
                    continue;
                }
            }
        }

        private static void CheckTypeDeclaration(SyntaxNodeAnalysisContext c)
        {
            var classDeclaration = (TypeDeclarationSyntax)c.Node;
            var classSymbol = c.SemanticModel.GetDeclaredSymbol(classDeclaration);

            if (classSymbol == null ||
                !classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) ||
                classSymbol.DeclaringSyntaxReferences.Count() > 1)
            {
                return;
            }

            var keyword = classDeclaration.Modifiers.First(m => m.IsKind(SyntaxKind.PartialKeyword));
            c.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation(), "partial", "gratuitous"));
        }

        private static void CheckMethodOrProperty(SyntaxNodeAnalysisContext c)
        {
            var memberDeclaration = (MemberDeclarationSyntax)c.Node;
            var memberSymbol = c.SemanticModel.GetDeclaredSymbol(memberDeclaration);
            if (memberSymbol == null)
            {
                return;
            }

            CheckSealedMemberInSealedClass(c, memberDeclaration, memberSymbol);
            CheckVirtualMemberInSealedClass(c, memberDeclaration, memberSymbol);
        }

        private static readonly Accessibility[] DeniedDeclaredAccessibility = { Accessibility.Public, Accessibility.Protected };

        private static bool HasDerivedClass(INamedTypeSymbol analyzedClass, ImmutableDictionary<INamedTypeSymbol, INamedTypeSymbol> classWithBaseType)
        {
            return classWithBaseType.Values.Any(derived => analyzedClass.Equals(derived.OriginalDefinition));
        }

        private static SyntaxTokenList GetModifiers(MemberDeclarationSyntax memberDeclaration)
        {
            var method = memberDeclaration as MethodDeclarationSyntax;
            if (method != null)
            {
                return method.Modifiers;
            }

            var property = memberDeclaration as PropertyDeclarationSyntax;
            if (property != null)
            {
                return property.Modifiers;
            }

            return default(SyntaxTokenList);
        }

        private static void CheckSealedMemberInSealedClass(SyntaxNodeAnalysisContext c,
            MemberDeclarationSyntax memberDeclaration, ISymbol memberSymbol)
        {
            if (!memberSymbol.IsSealed ||
                !memberSymbol.ContainingType.IsSealed)
            {
                return;
            }

            var modifiers = GetModifiers(memberDeclaration);
            if (modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword)))
            {
                var keyword = modifiers.First(m => m.IsKind(SyntaxKind.SealedKeyword));
                c.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation(), "sealed", "redundant"));
            }
        }
        private static void CheckVirtualMemberInSealedClass(SyntaxNodeAnalysisContext c,
            MemberDeclarationSyntax memberDeclaration, ISymbol memberSymbol)
        {
            if (!memberSymbol.IsVirtual ||
                !memberSymbol.ContainingType.IsSealed)
            {
                return;
            }

            var modifiers = GetModifiers(memberDeclaration);
            if (modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)))
            {
                var keyword = modifiers.First(m => m.IsKind(SyntaxKind.VirtualKeyword));
                c.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation(), "virtual", "gratuitous"));
            }
        }
    }
}
