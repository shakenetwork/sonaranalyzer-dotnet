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

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class GetTypeWithIsAssignableFrom : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2219";
        internal const string Title = "Runtime type checking should be simplified";
        internal const string Description =
            "To check the type of an object there are several options: \"is\", \"IsInstanceOfType\" or \"IsAssignableFrom\". Depending on whether " +
            "the type is returned by a \"GetType()\" or \"typeof()\" call, the \"IsAssignableFrom()\" and \"IsInstanceOfType()\" might be simplified. " +
            "Simplifying the calls make \"null\" checking unnecessary because both \"is\" and \"IsInstanceOfType\" performs it already.";
        internal const string MessageFormat = "Use the {0} instead.";
        internal const string IsOperator = "\"is\" operator";
        internal const string IsInstanceOfType = "\"IsInstanceOfType()\" method";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        internal const string UseIsOperatorKey = "UseIsOperator";
        internal const string ShouldRemoveGetType = "ShouldRemoveGetType";

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax)c.Node;
                    var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
                    if (memberAccess == null)
                    {
                        return;
                    }

                    var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (methodSymbol == null ||
                        methodSymbol.ContainingType.ToDisplayString() != "System.Type" ||
                        invocation.ArgumentList.Arguments.Count != 1)
                    {
                        return;
                    }

                    var argument = invocation.ArgumentList.Arguments.First().Expression;
                    CheckForIsAssignableFrom(c, invocation, memberAccess, methodSymbol, argument);
                    CheckForIsInstanceOfType(c, invocation, memberAccess, methodSymbol);
                },
                SyntaxKind.InvocationExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binary = (BinaryExpressionSyntax)c.Node;
                    CheckGetTypeAndTypeOfEquality(binary.Left, binary.Right, binary.GetLocation(), c);
                    CheckGetTypeAndTypeOfEquality(binary.Right, binary.Left, binary.GetLocation(), c);
                },
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);
        }

        private static void CheckGetTypeAndTypeOfEquality(ExpressionSyntax sideA, ExpressionSyntax sideB, Location location,
            SyntaxNodeAnalysisContext context)
        {
            if (!IsGetTypeCall(sideA, context.SemanticModel))
            {
                return;
            }

            var typeSyntax = (sideB as TypeOfExpressionSyntax)?.Type;
            if (typeSyntax == null)
            {
                return;
            }

            var typeSymbol = context.SemanticModel.GetTypeInfo(typeSyntax).Type;
            if (typeSymbol == null || !typeSymbol.IsSealed)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, IsOperator));
        }

        private static void CheckForIsInstanceOfType(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess, IMethodSymbol methodSymbol)
        {
            if (methodSymbol.Name != "IsInstanceOfType")
            {
                return;
            }

            if (memberAccess.Expression is TypeOfExpressionSyntax)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                    ImmutableDictionary<string, string>.Empty
                        .Add(UseIsOperatorKey, true.ToString())
                        .Add(ShouldRemoveGetType, false.ToString()),
                    IsOperator));
            }
        }

        private static void CheckForIsAssignableFrom(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess, IMethodSymbol methodSymbol,
            ExpressionSyntax argument)
        {
            if (methodSymbol.Name != "IsAssignableFrom" ||
                !IsGetTypeCall(argument, context.SemanticModel))
            {
                return;
            }

            if (memberAccess.Expression is TypeOfExpressionSyntax)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                    ImmutableDictionary<string, string>.Empty
                        .Add(UseIsOperatorKey, true.ToString())
                        .Add(ShouldRemoveGetType, true.ToString()),
                    IsOperator));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                    ImmutableDictionary<string, string>.Empty
                        .Add(UseIsOperatorKey, false.ToString())
                        .Add(ShouldRemoveGetType, true.ToString()),
                    IsInstanceOfType));
            }
        }

        private static bool IsGetTypeCall(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var invocation = expression as InvocationExpressionSyntax;
            if (invocation == null)
            {
                return false;
            }

            var methodCall = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodCall == null)
            {
                return false;
            }

            return methodCall.Name == "GetType" &&
                methodCall.ContainingType.SpecialType == SpecialType.System_Object;
        }
    }
}
