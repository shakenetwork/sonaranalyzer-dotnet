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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Design)]
    public class ClassWithOnlyStaticMember : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1118";
        internal const string Title = "Utility classes should not have public constructors";
        internal const string Description =
            "Utility classes, which are collections of \"static\" members, are not meant to be instantiated. Even \"abstract\" " +
            "utility classes, which can be extended, should not have \"public\" constructors. C# adds an implicit public " +
            "constructor to every class which does not explicitly define at least one constructor. Hence, at least one " +
            "\"protected\" constructor should be defined if you wish to subclass this utility class. Or the \"static\" keyword " +
            "should be added to the class declaration to prevent subclassing.";
        internal const string MessageFormatConstructor = "Hide this public constructor by making it \"{0}\".";
        internal const string MessageFormatStaticClass =
            "Add a \"{0}\" constructor or the \"static\" keyword to the class declaration.";
        internal const string Category = SonarAnalyzer.Common.Category.Design;
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

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var namedType = c.Symbol as INamedTypeSymbol;
                    if (!namedType.IsClass())
                    {
                        return;
                    }

                    CheckClasses(namedType, c);
                    CheckConstructors(namedType, c);
                },
                SymbolKind.NamedType);
        }

        private static void CheckClasses(INamedTypeSymbol utilityClass, SymbolAnalysisContext context)
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
                    context.ReportDiagnosticIfNonGenerated(
                        Diagnostic.Create(Rule, classDeclarationSyntax.Identifier.GetLocation(), reportMessage),
                        context.Compilation);
                }
            }
        }

        private static readonly Accessibility[] ProblematicConstructorAccessibility =
        {
            Accessibility.Public,
            Accessibility.Internal
        };

        private static void CheckConstructors(INamedTypeSymbol utilityClass, SymbolAnalysisContext context)
        {
            if (!ClassQualifiesForIssue(utilityClass) ||
                !HasMembersAndAllAreStaticExceptConstructors(utilityClass))
            {
                return;
            }

            var reportMessage = string.Format(MessageFormatConstructor,
                utilityClass.IsSealed ? "private" : "protected");

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
                        context.ReportDiagnosticIfNonGenerated(
                            Diagnostic.Create(Rule, constructorDeclaration.Identifier.GetLocation(), reportMessage),
                            context.Compilation);
                    }
                }
            }
        }

        private static bool ClassIsRelevant(INamedTypeSymbol @class)
        {
            return ClassQualifiesForIssue(@class) &&
                   HasOnlyQualifyingMembers(@class, @class.GetMembers()
                .Where(member => !member.IsImplicitlyDeclared)
                .ToList());
        }

        private static bool ClassQualifiesForIssue(INamedTypeSymbol @class)
        {
            return !@class.IsStatic &&
                   !@class.AllInterfaces.Any() &&
                   @class.BaseType.Is(KnownType.System_Object);
        }

        private static bool HasOnlyQualifyingMembers(INamedTypeSymbol @class, IList<ISymbol> members)
        {
            return members.Any() &&
                   members.All(member => member.IsStatic) &&
                   !ClassUsedAsInstanceInMembers(@class, members);
        }

        private static bool ClassUsedAsInstanceInMembers(INamedTypeSymbol @class, IList<ISymbol> members)
        {
            return members.OfType<IMethodSymbol>().Any(member =>
                        @class.Equals(member.ReturnType) ||
                        member.Parameters.Any(parameter => @class.Equals(parameter.Type))) ||
                   members.OfType<IPropertySymbol>().Any(member => @class.Equals(member.Type)) ||
                   members.OfType<IFieldSymbol>().Any(member => @class.Equals(member.Type));
        }

        private static bool HasMembersAndAllAreStaticExceptConstructors(INamedTypeSymbol @class)
        {
            var membersExceptConstructors = @class.GetMembers()
                .Where(member => !IsConstructor(member))
                .ToList();

            return HasOnlyQualifyingMembers(@class, membersExceptConstructors);
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
