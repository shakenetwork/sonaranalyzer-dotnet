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
using System.Linq;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Cert, Tag.Cwe, Tag.Misra, Tag.Pitfall)]
    public class InvalidCastToInterface : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1944";
        internal const string Title = "Inappropriate casts should not be made";
        internal const string Description =
            "Inappropriate casts are issues that will lead to unexpected behavior or runtime errors, such as " +
            "\"InvalidCastException\"s. The compiler will catch bad casts from one class to another, but not " +
            "bad casts to interfaces.";
        internal const string MessageFormat = "Review this cast; in this project there's no type that {0}.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterCompilationStartAction(
                compilationStartContext =>
                {
                    var allNamedTypeSymbols = compilationStartContext.Compilation.GlobalNamespace.GetAllNamedTypes();
                    var typeInterfaceMappings = allNamedTypeSymbols.Select(type =>
                        new
                        {
                            Type = type.OriginalDefinition,
                            Interfaces = type.OriginalDefinition.AllInterfaces.Select(i => i.OriginalDefinition)
                        });

                    var interfaceImplementerMappings = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>();
                    foreach (var typeInterfaceMapping in typeInterfaceMappings)
                    {
                        if (typeInterfaceMapping.Type.IsInterface())
                        {
                            if (!interfaceImplementerMappings.ContainsKey(typeInterfaceMapping.Type))
                            {
                                interfaceImplementerMappings.Add(typeInterfaceMapping.Type, new HashSet<INamedTypeSymbol>());
                            }

                            interfaceImplementerMappings[typeInterfaceMapping.Type].Add(typeInterfaceMapping.Type);
                        }

                        foreach (var @interface in typeInterfaceMapping.Interfaces)
                        {
                            if (!interfaceImplementerMappings.ContainsKey(@interface))
                            {
                                interfaceImplementerMappings.Add(@interface, new HashSet<INamedTypeSymbol>());
                            }

                            interfaceImplementerMappings[@interface].Add(typeInterfaceMapping.Type);
                        }
                    }

                    compilationStartContext.RegisterSyntaxNodeActionInNonGenerated(
                        c =>
                        {
                            var cast = (CastExpressionSyntax)c.Node;
                            var interfaceType = c.SemanticModel.GetTypeInfo(cast.Type).Type as INamedTypeSymbol;
                            var expressionType = c.SemanticModel.GetTypeInfo(cast.Expression).Type as INamedTypeSymbol;

                            CheckTypesForInvalidCast(interfaceType, expressionType, interfaceImplementerMappings,
                                cast.Type.GetLocation(), c);
                        },
                        SyntaxKind.CastExpression);

                    compilationStartContext.RegisterSyntaxNodeAction(
                        c =>
                        {
                            var cast = (BinaryExpressionSyntax)c.Node;
                            var interfaceType = c.SemanticModel.GetTypeInfo(cast.Right).Type as INamedTypeSymbol;
                            var expressionType = c.SemanticModel.GetTypeInfo(cast.Left).Type as INamedTypeSymbol;

                            CheckTypesForInvalidCast(interfaceType, expressionType, interfaceImplementerMappings,
                                cast.Right.GetLocation(), c);
                        },
                        SyntaxKind.AsExpression,
                        SyntaxKind.IsExpression);
                });
        }

        private static void CheckTypesForInvalidCast(INamedTypeSymbol interfaceType, INamedTypeSymbol expressionType,
            Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> interfaceImplementerMappings, Location issueLocation,
            SyntaxNodeAnalysisContext context)
        {
            if (interfaceType == null ||
                expressionType == null ||
                !interfaceType.IsInterface() ||
                expressionType.Is(KnownType.System_Object))
            {
                return;
            }

            if (!HasExistingConcreteImplementation(interfaceType, interfaceImplementerMappings))
            {
                return;
            }

            if (expressionType.IsInterface() &&
                !HasExistingConcreteImplementation(expressionType, interfaceImplementerMappings))
            {
                return;
            }

            if (interfaceImplementerMappings.ContainsKey(interfaceType.OriginalDefinition) &&
                !interfaceImplementerMappings[interfaceType.OriginalDefinition].Any(t => t.DerivesOrImplements(expressionType.OriginalDefinition)))
            {
                ReportIssue(interfaceType, expressionType, issueLocation, context);
            }
        }

        private static bool HasExistingConcreteImplementation(INamedTypeSymbol type,
            Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> interfaceImplementerMappings)
        {
            return interfaceImplementerMappings.ContainsKey(type) &&
                interfaceImplementerMappings[type].Any(t => t.IsClassOrStruct());
        }

        private static void ReportIssue(INamedTypeSymbol interfaceType, INamedTypeSymbol expressionType, Location issueLocation,
            SyntaxNodeAnalysisContext context)
        {
            var interfaceTypeName = interfaceType.ToMinimalDisplayString(context.SemanticModel, issueLocation.SourceSpan.Start);
            var expressionTypeName = expressionType.ToMinimalDisplayString(context.SemanticModel, issueLocation.SourceSpan.Start);

            var messageReasoning = expressionType.IsInterface()
                ? string.Format("implements both \"{0}\" and \"{1}\"", expressionTypeName, interfaceTypeName)
                : string.Format("extends \"{0}\" and implements \"{1}\"", expressionTypeName, interfaceTypeName);

            context.ReportDiagnostic(Diagnostic.Create(Rule, issueLocation, messageReasoning));
        }
    }
}
