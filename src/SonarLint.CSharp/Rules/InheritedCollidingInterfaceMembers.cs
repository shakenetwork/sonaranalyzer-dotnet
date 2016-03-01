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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Design)]
    public class InheritedCollidingInterfaceMembers : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3444";
        internal const string Title = "Interfaces with colliding, inherited members should explicitly redefine interface members";
        internal const string Description =
            "When an interface \"IDerived\" inherits from two interfaces \"IBase1\" and \"IBase2\" that both define a member " +
            "\"SomeProperty\", calling \"IDerived.SomeProperty\" will result in the compiler error \"CS0229 Ambiguity between " +
            "'IBase1.SomeProperty' and 'IBase2.SomeProperty'\". Every caller will be forced to cast instances of \"IDerived\" " +
            "to \"IBase1\" or \"IBase2\" to resolve the ambiguity and to be able to access \"SomeProperty\". Instead, it is " +
            "better to resolve the ambiguity on the definition of \"IDerived\" either by: renaming one of the \"SomeProperty\" " +
            "in \"IBase1\" or \"IBase2\" to remove the collision or by also defining a new \"SomeProperty\" member on " +
            "\"IDerived\". Use the latter only if all \"SomeProperty\" are meant to hold the same value.";
        internal const string MessageFormat = "Rename or add member{1} {0} to this interface to resolve ambiguities.";
        internal const string Category = SonarLint.Common.Category.Design;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const int MaxMemberDisplayCount = 2;

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var interfaceDeclaration = (InterfaceDeclarationSyntax)c.Node;
                    if (interfaceDeclaration.BaseList == null ||
                        interfaceDeclaration.BaseList.Types.Count < 2)
                    {
                        return;
                    }

                    var interfaceSymbol = c.SemanticModel.GetDeclaredSymbol(interfaceDeclaration);
                    if (interfaceSymbol == null)
                    {
                        return;
                    }

                    var collidingMembers = GetCollidingMembers(interfaceSymbol)
                        .Take(MaxMemberDisplayCount + 1)
                        .ToList();

                    if (collidingMembers.Any())
                    {
                        var membersText = GetIssueMessageText(collidingMembers, c.SemanticModel, interfaceDeclaration.SpanStart);
                        var pluralize = collidingMembers.Count > 1 ? "s" : string.Empty;

                        c.ReportDiagnostic(Diagnostic.Create(Rule, interfaceDeclaration.Identifier.GetLocation(),
                            membersText, pluralize));
                    }
                },
                SyntaxKind.InterfaceDeclaration);
        }
        
        private static IEnumerable<IMethodSymbol> GetCollidingMembers(INamedTypeSymbol interfaceSymbol)
        {
            var interfacesToCheck = interfaceSymbol.Interfaces;

            var membersFromDerivedInterface = interfaceSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .ToList();

            for (int i = 0; i < interfacesToCheck.Length; i++)
            {
                var notRedefinedMembersFromInterface1 = interfacesToCheck[i].GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(method => !membersFromDerivedInterface.Any(redefinedMember => AreCollidingMethods(method, redefinedMember)));

                foreach (var notRedefinedMemberFromInterface1 in notRedefinedMembersFromInterface1)
                {
                    for (int j = i + 1; j < interfacesToCheck.Length; j++)
                    {
                        var collidingMembersFromInterface2 = interfacesToCheck[j]
                            .GetMembers(notRedefinedMemberFromInterface1.Name)
                            .OfType<IMethodSymbol>()
                            .Where(methodSymbol2 => IsNotEventRemoveAccessor(methodSymbol2))
                            .Where(methodSymbol2 => AreCollidingMethods(notRedefinedMemberFromInterface1, methodSymbol2));

                        foreach (var collidingMember in collidingMembersFromInterface2)
                        {
                            yield return collidingMember;
                        }
                    }
                }
            }
        }

        private static bool IsNotEventRemoveAccessor(IMethodSymbol methodSymbol2)
        {
            /// we only want to report on events once, so we are not collecting the "remove" accessors,
            /// and handle the the "add" accessor reporting separately in <see cref="GetMemberDisplayName"/>
            return methodSymbol2.MethodKind != MethodKind.EventRemove;
        }

        private static string GetIssueMessageText(ICollection<IMethodSymbol> collidingMembers, SemanticModel semanticModel,
            int spanStart)
        {
            var names = collidingMembers
                .Take(MaxMemberDisplayCount)
                .Select(member => GetMemberDisplayName(member, spanStart, semanticModel))
                .ToList();

            if (collidingMembers.Count == 1)
            {
                return names[0];
            }
            else if (collidingMembers.Count == 2)
            {
                return $"{names[0]} and {names[1]}";
            }
            else
            {
                names.Add("...");
                return string.Join(", ", names);
            }
        }

        private static readonly IImmutableSet<SymbolDisplayPartKind> PartKindsToStartWith = new []
            {
                SymbolDisplayPartKind.MethodName,
                SymbolDisplayPartKind.PropertyName,
                SymbolDisplayPartKind.EventName
            }.ToImmutableHashSet();

        private static string GetMemberDisplayName(IMethodSymbol method, int spanStart, SemanticModel semanticModel)
        {
            var parts = method.ToMinimalDisplayParts(semanticModel, spanStart, SymbolDisplayFormat.CSharpShortErrorMessageFormat)
                .SkipWhile(part => !PartKindsToStartWith.Contains(part.Kind))
                .ToList();

            if (method.MethodKind == MethodKind.EventAdd)
            {
                parts = parts.Take(parts.Count - 2).ToList();
            }

            return $"\"{string.Join(string.Empty, parts)}\"";
        }

        private static bool AreCollidingMethods(IMethodSymbol methodSymbol1, IMethodSymbol methodSymbol2)
        {
            if (methodSymbol1.Name != methodSymbol2.Name ||
                methodSymbol1.MethodKind != methodSymbol2.MethodKind ||
                methodSymbol1.Parameters.Length != methodSymbol2.Parameters.Length ||
                methodSymbol1.Arity != methodSymbol2.Arity)
            {
                return false;
            }
            
            for (int i = 0; i < methodSymbol1.Parameters.Length; i++)
            {
                var param1 = methodSymbol1.Parameters[i];
                var param2 = methodSymbol2.Parameters[i];
                
                if (param1.RefKind != param2.RefKind ||
                    !object.Equals(param1.Type, param2.Type))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
