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
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Globalization;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("1min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class RedundantInheritanceList : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1939";
        internal const string Title = "Inheritance list should not be redundant";
        internal const string Description =
            "Redundant declarations should be removed because they needlessly clutter the code and can be confusing.";
        internal const string MessageEnum = "\"int\" should not be explicitly used as the underlying type.";
        internal const string MessageObjectBase = "\"Object\" should not be explicitly extended.";
        internal const string MessageAlreadyImplements = "\"{0}\" is a \"{1}\" so \"{1}\" can be removed from the inheritance list.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, "{0}", Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        internal const string RedundantIndexKey = "redundantIndex";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckEnum(c),
                SyntaxKind.EnumDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckInterface(c),
                SyntaxKind.InterfaceDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckClass(c),
                SyntaxKind.ClassDeclaration);
        }

        private static void CheckClass(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            if (classDeclaration.BaseList == null ||
                classDeclaration.BaseList.Types.Count == 0)
            {
                return;
            }

            var baseTypeSyntax = classDeclaration.BaseList.Types.First().Type;
            var baseTypeSymbol = context.SemanticModel.GetSymbolInfo(baseTypeSyntax).Symbol as ITypeSymbol;
            if (baseTypeSymbol == null)
            {
                return;
            }

            if (baseTypeSymbol.Is(KnownType.System_Object))
            {
                var location = GetLocationWithToken(baseTypeSyntax, classDeclaration.BaseList.Types);
                context.ReportDiagnostic(Diagnostic.Create(Rule, location,
                    ImmutableDictionary<string, string>.Empty.Add(RedundantIndexKey, "0"),
                    MessageObjectBase));
            }

            CheckIfInterfaceIsRedundantForClass(context, classDeclaration);
        }

        private static void CheckInterface(SyntaxNodeAnalysisContext context)
        {
            var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;
            if (interfaceDeclaration.BaseList == null ||
                interfaceDeclaration.BaseList.Types.Count == 0)
            {
                return;
            }

            CheckIfInterfaceIsRedundantForInterface(context, interfaceDeclaration);
        }

        private static void CheckEnum(SyntaxNodeAnalysisContext context)
        {
            var enumDeclaration = (EnumDeclarationSyntax)context.Node;
            if (enumDeclaration.BaseList == null ||
                enumDeclaration.BaseList.Types.Count == 0)
            {
                return;
            }

            var baseTypeSyntax = enumDeclaration.BaseList.Types.First().Type;
            var baseTypeSymbol = context.SemanticModel.GetSymbolInfo(baseTypeSyntax).Symbol as ITypeSymbol;
            if (!baseTypeSymbol.Is(KnownType.System_Int32))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, enumDeclaration.BaseList.GetLocation(),
                ImmutableDictionary<string, string>.Empty.Add(RedundantIndexKey, "0"),
                MessageEnum));
        }

        private static void CheckIfInterfaceIsRedundantForInterface(SyntaxNodeAnalysisContext context, InterfaceDeclarationSyntax interfaceDeclaration)
        {
            CheckIfInterfaceIsRedundant(context, interfaceDeclaration.BaseList, interfaceType => false);
        }

        private static void CheckIfInterfaceIsRedundantForClass(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
        {
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
            if (classSymbol == null)
            {
                return;
            }

            CheckIfInterfaceIsRedundant(context, classDeclaration.BaseList,
                interfaceType => HasInterfaceMember(classSymbol, interfaceType));
        }

        private static void CheckIfInterfaceIsRedundant(SyntaxNodeAnalysisContext context, BaseListSyntax baseList,
            Predicate<INamedTypeSymbol> additionalCheck)
        {
            var interfaceTypesWithAllInterfaces =
                GetImplementedInterfaceMappings(baseList, context.SemanticModel);

            for (int i = 0; i < baseList.Types.Count; i++)
            {
                var baseType = baseList.Types[i];
                var interfaceType = context.SemanticModel.GetSymbolInfo(baseType.Type).Symbol as INamedTypeSymbol;
                if (!interfaceType.IsInterface())
                {
                    continue;
                }

                foreach (var interfaceTypeWithAllInterfaces in interfaceTypesWithAllInterfaces)
                {
                    if (interfaceTypeWithAllInterfaces.Value.Contains(interfaceType) &&
                        !additionalCheck(interfaceType))
                    {
                        var location = GetLocationWithToken(baseType.Type, baseList.Types);
                        context.ReportDiagnostic(Diagnostic.Create(Rule, location,
                            ImmutableDictionary<string, string>.Empty.Add(RedundantIndexKey, i.ToString(CultureInfo.InvariantCulture)),
                            string.Format(MessageAlreadyImplements, interfaceTypeWithAllInterfaces.Key.Name, interfaceType.Name)));
                        break;
                    }
                }
            }
        }

        private static MultiValueDictionary<INamedTypeSymbol, INamedTypeSymbol> GetImplementedInterfaceMappings(
            BaseListSyntax baseList, SemanticModel semanticModel)
        {
            return baseList.Types
                .Select(baseType => semanticModel.GetSymbolInfo(baseType.Type).Symbol as INamedTypeSymbol)
                .Where(symbol => symbol != null)
                .Select(symbol => new Tuple<INamedTypeSymbol, ICollection<INamedTypeSymbol>>(symbol, symbol.AllInterfaces))
                .ToMultiValueDictionary(kv => kv.Item1, kv => kv.Item2);
        }

        private static bool HasInterfaceMember(INamedTypeSymbol classSymbol, INamedTypeSymbol interfaceType)
        {
            return interfaceType.GetMembers().Any(interfaceMember =>
            {
                var classMember = classSymbol.FindImplementationForInterfaceMember(interfaceMember);
                return classMember != null &&
                    classMember.ContainingType.Equals(classSymbol);
            });
        }

        private static Location GetLocationWithToken(TypeSyntax type, SeparatedSyntaxList<BaseTypeSyntax> baseTypes)
        {
            int start;
            int end;

            if (baseTypes.Count == 1 ||
                baseTypes.First().Type != type)
            {
                start = type.GetFirstToken().GetPreviousToken().Span.Start;
                end = type.Span.End;
            }
            else
            {
                start = type.SpanStart;
                end = type.GetLastToken().GetNextToken().Span.End;
            }

            return Location.Create(type.SyntaxTree, new TextSpan(start, end - start));
        }
    }
}
