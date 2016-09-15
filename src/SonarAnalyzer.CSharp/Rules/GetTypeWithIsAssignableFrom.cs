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

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Helpers.CSharp;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class GetTypeWithIsAssignableFrom : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2219";
        internal const string Title = "Runtime type checking should be simplified";
        internal const string Description =
            "To check the type of an object there are several options: \"is\", \"IsInstanceOfType\" or \"IsAssignableFrom\". Depending on whether " +
            "the type is returned by a \"GetType()\" or \"typeof()\" call, the \"IsAssignableFrom()\" and \"IsInstanceOfType()\" might be simplified. " +
            "Simplifying the calls make \"null\" checking unnecessary because both \"is\" and \"IsInstanceOfType\" performs it already.";
        internal const string MessageFormat = "Use {0} instead.";
        internal const string MessageIsOperator = "the \"is\" operator";
        internal const string MessageIsInstanceOfType = "the \"IsInstanceOfType()\" method";
        internal const string MessageNullCheck = "a \"null\" check";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
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

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax)c.Node;
                    var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
                    if (memberAccess == null ||
                        !invocation.HasExactlyNArguments(1))
                    {
                        return;
                    }

                    var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (!methodSymbol.IsInType(KnownType.System_Type))
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

                    CheckAsOperatorComparedToNull(binary.Left, binary.Right, binary.GetLocation(), c);
                    CheckAsOperatorComparedToNull(binary.Right, binary.Left, binary.GetLocation(), c);
                },
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckIfIsExpressionCanBeReplacedByNullCheck(c),
                SyntaxKind.IsExpression);
        }

        private static void CheckIfIsExpressionCanBeReplacedByNullCheck(SyntaxNodeAnalysisContext context)
        {
            var binary = (BinaryExpressionSyntax)context.Node;
            var typeExpression = context.SemanticModel.GetTypeInfo(binary.Left).Type;
            var typeCastTo = context.SemanticModel.GetTypeInfo(binary.Right).Type;

            if (typeExpression == null ||
                typeCastTo == null ||
                !typeExpression.IsClass() ||
                !typeCastTo.IsClass())
            {
                return;
            }

            if (typeExpression.DerivesOrImplements(typeCastTo))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, binary.GetLocation(), MessageNullCheck));
            }
        }

        private static void CheckAsOperatorComparedToNull(ExpressionSyntax sideA, ExpressionSyntax sideB, Location location,
            SyntaxNodeAnalysisContext context)
        {
            if (!EquivalenceChecker.AreEquivalent(sideA.RemoveParentheses(), SyntaxHelper.NullLiteralExpression))
            {
                return;
            }

            if (!sideB.RemoveParentheses().IsKind(SyntaxKind.AsExpression))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, MessageIsOperator));
        }

        private static void CheckGetTypeAndTypeOfEquality(ExpressionSyntax sideA, ExpressionSyntax sideB, Location location,
            SyntaxNodeAnalysisContext context)
        {
            if (!TypeExaminationOnSystemType.IsGetTypeCall(sideA as InvocationExpressionSyntax, context.SemanticModel))
            {
                return;
            }

            var typeSyntax = (sideB as TypeOfExpressionSyntax)?.Type;
            if (typeSyntax == null)
            {
                return;
            }

            var typeSymbol = context.SemanticModel.GetTypeInfo(typeSyntax).Type;
            if (typeSymbol == null ||
                !typeSymbol.IsSealed ||
                typeSymbol.OriginalDefinition.Is(KnownType.System_Nullable_T))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, MessageIsOperator));
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
                    MessageIsOperator));
            }
        }

        private static void CheckForIsAssignableFrom(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess, IMethodSymbol methodSymbol,
            ExpressionSyntax argument)
        {
            if (methodSymbol.Name != "IsAssignableFrom" ||
                !TypeExaminationOnSystemType.IsGetTypeCall(argument as InvocationExpressionSyntax, context.SemanticModel))
            {
                return;
            }

            if (memberAccess.Expression is TypeOfExpressionSyntax)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                    ImmutableDictionary<string, string>.Empty
                        .Add(UseIsOperatorKey, true.ToString())
                        .Add(ShouldRemoveGetType, true.ToString()),
                    MessageIsOperator));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                    ImmutableDictionary<string, string>.Empty
                        .Add(UseIsOperatorKey, false.ToString())
                        .Add(ShouldRemoveGetType, true.ToString()),
                    MessageIsInstanceOfType));
            }
        }
    }
}
