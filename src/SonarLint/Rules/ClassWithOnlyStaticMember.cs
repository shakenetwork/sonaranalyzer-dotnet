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

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("design")]
    public class ClassWithOnlyStaticMember : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1118";
        internal const string Title = "Utility classes should not have public constructors";

        internal const string Description =
            "Utility classes, which are collections of \"static\" members, are not meant to be instantiated. Even \"abstract\" " +
            "utility classes, which can be extended, should not have \"public\" constructors. C# adds an implicit public " +
            "constructor to every class which does not explicitly define at least one constructor. Hence, at least one " +
            "\"protected\" constructor should be defined if you wish to subclass this utility class. Or the \"static\" keyword " +
            "should be added to the class declaration to prevent subclassing.";

        internal const string MessageFormatConstructor = "Hide this public constructor.";

        internal const string MessageFormatStaticClass =
            "Add a \"{0}\" constructor or the \"static\" keyword to the class declaration.";

        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, "{0}", Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var namedType = c.Symbol as INamedTypeSymbol;
                    if (namedType == null ||
                        !namedType.IsType ||
                        namedType.TypeKind != TypeKind.Class)
                    {
                        return;
                    }

                    CheckClasses(namedType, c);
                    CheckConstructors(namedType, c);
                }, 
                SymbolKind.NamedType);
        }

        private static void CheckClasses(INamedTypeSymbol utilityClass, SymbolAnalysisContext c)
        {
            if (!ClassIsRelevant(utilityClass))
            {
                return;
            }

            var reportMessage = string.Format(MessageFormatStaticClass,
                utilityClass.IsSealed ? "private" : "protected");

            foreach (var syntaxReference in utilityClass.DeclaringSyntaxReferences)
            {
                var classDeclarationSyntax = syntaxReference.GetSyntax() as ClassDeclarationSyntax;
                if (classDeclarationSyntax != null)
                {
                    c.ReportDiagnostic(Diagnostic.Create(Rule, classDeclarationSyntax.Identifier.GetLocation(),
                        reportMessage));
                }
            }
        }

        private static readonly Accessibility[] ProblematicConstructorAccessibility =
        {
            Accessibility.Public,
            Accessibility.Internal
        };

        private static void CheckConstructors(INamedTypeSymbol utilityClass, SymbolAnalysisContext c)
        {
            if (!ClassQualifiesForIssue(utilityClass) ||
                !HasMembersAndAllAreStaticExceptConstructors(utilityClass.GetMembers()))
            {
                return;
            }

            foreach (var constructor in utilityClass.GetMembers()
                .Where(IsConstructor)
                .Where(symbol => ProblematicConstructorAccessibility.Contains(symbol.DeclaredAccessibility)))
            {
                var syntaxReferences = constructor.DeclaringSyntaxReferences;
                foreach (var syntaxReference in syntaxReferences)
                {
                    var constructorDeclaration = syntaxReference.GetSyntax() as ConstructorDeclarationSyntax;
                    if (constructorDeclaration != null)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, constructorDeclaration.Identifier.GetLocation(),
                            MessageFormatConstructor));
                    }
                }
            }
        }

        private static bool ClassIsRelevant(INamedTypeSymbol @class)
        {
            return ClassQualifiesForIssue(@class) &&
                   HasMembersAndAllAreStatic(@class.GetMembers()
                       .Where(member => !member.IsImplicitlyDeclared).ToImmutableList());
        }

        private static bool ClassQualifiesForIssue(INamedTypeSymbol @class)
        {
            return !@class.IsStatic &&
                   !@class.AllInterfaces.Any() &&
                   @class.BaseType.SpecialType == SpecialType.System_Object;
        }

        private static bool HasMembersAndAllAreStatic(IImmutableList<ISymbol> members)
        {
            return members.Any() &&
                   members.All(member => member.IsStatic);
        }

        private static bool HasMembersAndAllAreStaticExceptConstructors(IImmutableList<ISymbol> members)
        {
            var membersExceptConstructors = members
                .Where(member => !IsConstructor(member))
                .ToImmutableList();

            return HasMembersAndAllAreStatic(membersExceptConstructors);
        }

        private static bool IsConstructor(ISymbol member)
        {
            var method = member as IMethodSymbol;
            return method != null &&
                   method.MethodKind == MethodKind.Constructor &&
                   !method.IsImplicitlyDeclared;
        }
    }
}
