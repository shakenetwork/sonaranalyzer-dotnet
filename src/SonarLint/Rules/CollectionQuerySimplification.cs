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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class CollectionQuerySimplification : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2971";
        internal const string Title = "\"IEnumerable\" LINQs should be simplified";
        internal const string Description =
            "In the interests of readability, code that can be simplified should be simplified. To that end, there are several " +
            "ways \"IEnumerable LINQ\"s can be simplified. Use \"OfType\" instead of using \"Select\" with \"as\" to type cast " +
            "elements and then null-checking in a query expression to choose elements based on type. Use \"OfType\" instead of " +
            "using \"Where\" and the \"is\" operator, followed by a cast in a \"Select\". Use an expression in \"Any\" instead " +
            "of \"Where(element => [expression]).Any()\".";
        internal const string MessageFormatType = "Use \"OfType<{0}>()\" here instead.";
        internal const string MessageFormatSimplify = "Drop \"{0}\" and move the condition into the \"{1}\".";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, "{0}", Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        private static readonly ExpressionSyntax NullExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly string[] MethodNamesWithPredicate =
        {
            "Any", "LongCount", "Count",
            "First", "FirstOrDefault", "Last", "LastOrDefault",
            "Single", "SingleOrDefault"
        };
        private static readonly string[] MethodNamesForTypeCheckingWithSelect =
        {
            "Any", "LongCount", "Count",
            "First", "FirstOrDefault", "Last", "LastOrDefault",
            "Single", "SingleOrDefault", "SkipWhile", "TakeWhile"
        };

        private static readonly SyntaxKind[] AsIsSyntaxKinds = {SyntaxKind.AsExpression, SyntaxKind.IsExpression};
        private const string WhereMethodName = "Where";
        private const string SelectMethodName = "Select";

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var outerInvocation = (InvocationExpressionSyntax) c.Node;
                    var outerMethodSymbol = c.SemanticModel.GetSymbolInfo(outerInvocation).Symbol as IMethodSymbol;
                    if (outerMethodSymbol == null ||
                        !CollectionEmptinessChecking.MethodIsOnIEnumerable(outerMethodSymbol, c.SemanticModel))
                    {
                        return;
                    }

                    InvocationExpressionSyntax innerInvocation;
                    if (outerMethodSymbol.MethodKind == MethodKind.ReducedExtension)
                    {
                        var memberAccess = outerInvocation.Expression as MemberAccessExpressionSyntax;
                        if (memberAccess == null)
                        {
                            return;
                        }
                        innerInvocation = memberAccess.Expression as InvocationExpressionSyntax;
                    }
                    else
                    {
                        var argument = outerInvocation.ArgumentList.Arguments.FirstOrDefault();
                        if (argument == null)
                        {
                            return;
                        }
                        innerInvocation = argument.Expression as InvocationExpressionSyntax;
                    }

                    if (innerInvocation == null)
                    {
                        return;
                    }

                    var innerMethodSymbol = c.SemanticModel.GetSymbolInfo(innerInvocation).Symbol as IMethodSymbol;
                    if (innerMethodSymbol == null ||
                        !CollectionEmptinessChecking.MethodIsOnIEnumerable(innerMethodSymbol, c.SemanticModel))
                    {
                        return;
                    }

                    var outerArguments = GetReducedArguments(outerMethodSymbol, outerInvocation);

                    if (CheckForSimplifiable(outerArguments, outerMethodSymbol, innerMethodSymbol, c,
                        innerInvocation))
                    {
                        return;
                    }

                    if (CheckForCastSimplification(outerMethodSymbol, innerMethodSymbol, innerInvocation,
                        outerInvocation, c))
                    {
                        return;
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static List<ArgumentSyntax> GetReducedArguments(IMethodSymbol methodSymbol, InvocationExpressionSyntax invocation)
        {
            return methodSymbol.MethodKind == MethodKind.ReducedExtension
                ? invocation.ArgumentList.Arguments.ToList()
                : invocation.ArgumentList.Arguments.Skip(1).ToList();
        }

        private static bool CheckForCastSimplification(IMethodSymbol outerMethodSymbol, IMethodSymbol innerMethodSymbol,
            InvocationExpressionSyntax innerInvocation, InvocationExpressionSyntax outerInvocation, SyntaxNodeAnalysisContext c)
        {
            string typeNameInInner;
            if (MethodNamesForTypeCheckingWithSelect.Contains(outerMethodSymbol.Name) &&
                innerMethodSymbol.Name == SelectMethodName &&
                IsFirstExpressionInLambdaIsNullChecking(outerMethodSymbol, outerInvocation) &&
                TryGetCastInLambda(SyntaxKind.AsExpression, innerMethodSymbol, innerInvocation, out typeNameInInner))
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, GetReportLocation(innerInvocation),
                    string.Format(MessageFormatType, typeNameInInner)));
                return true;
            }

            string typeNameInOuter;
            if (outerMethodSymbol.Name == SelectMethodName &&
                innerMethodSymbol.Name == WhereMethodName &&
                IsExpressionInLambdaIsCast(outerMethodSymbol, outerInvocation, out typeNameInOuter) &&
                TryGetCastInLambda(SyntaxKind.IsExpression, innerMethodSymbol, innerInvocation, out typeNameInInner) &&
                typeNameInOuter == typeNameInInner)
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, GetReportLocation(innerInvocation),
                    string.Format(MessageFormatType, typeNameInInner)));
                return true;
            }

            return false;
        }

        private static Location GetReportLocation(InvocationExpressionSyntax invocation)
        {
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            return memberAccess == null
                ? invocation.Expression.GetLocation()
                : memberAccess.Name.GetLocation();
        }

        private static bool IsExpressionInLambdaIsCast(IMethodSymbol methodSymbol, InvocationExpressionSyntax invocation, out string typeName)
        {
            return TryGetCastInLambda(SyntaxKind.AsExpression, methodSymbol, invocation, out typeName) ||
                   TryGetCastInLambda(methodSymbol, invocation, out typeName);
        }

        private static bool IsFirstExpressionInLambdaIsNullChecking(IMethodSymbol methodSymbol, InvocationExpressionSyntax invocation)
        {
            var arguments = GetReducedArguments(methodSymbol, invocation);
            if (arguments.Count() != 1)
            {
                return false;
            }
            var expression = arguments.First().Expression;

            var binaryExpression = GetExpressionFromParens(GetExpressionFromLambda(expression)) as BinaryExpressionSyntax;
            var lambdaParameter = GetLambdaParameter(expression);

            while (binaryExpression != null)
            {
                if (!binaryExpression.IsKind(SyntaxKind.LogicalAndExpression))
                {
                    return binaryExpression.IsKind(SyntaxKind.NotEqualsExpression) &&
                           IsNullChecking(binaryExpression, lambdaParameter);
                }
                binaryExpression = GetExpressionFromParens(binaryExpression.Left) as BinaryExpressionSyntax;
            }
            return false;
        }

        private static bool IsNullChecking(BinaryExpressionSyntax binaryExpression, string lambdaParameter)
        {
            if (EquivalenceChecker.AreEquivalent(NullExpression, GetExpressionFromParens(binaryExpression.Left)) &&
                GetExpressionFromParens(binaryExpression.Right).ToString() == lambdaParameter)
            {
                return true;
            }
            if (EquivalenceChecker.AreEquivalent(NullExpression, GetExpressionFromParens(binaryExpression.Right)) &&
                GetExpressionFromParens(binaryExpression.Left).ToString() == lambdaParameter)
            {
                return true;
            }
            return false;
        }

        private static ExpressionSyntax GetExpressionFromParens(ExpressionSyntax expression)
        {
            var parens = expression as ParenthesizedExpressionSyntax;
            var current = expression;
            while (parens != null)
            {
                current = parens.Expression;
                parens = current as ParenthesizedExpressionSyntax;
            }

            return current;
        }

        private static ExpressionSyntax GetExpressionFromLambda(ExpressionSyntax expression)
        {
            ExpressionSyntax lambdaBody;
            var lambda = expression as SimpleLambdaExpressionSyntax;
            if (lambda == null)
            {
                var parenthesizedLambda = expression as ParenthesizedLambdaExpressionSyntax;
                if (parenthesizedLambda == null)
                {
                    return null;
                }
                lambdaBody = parenthesizedLambda.Body as ExpressionSyntax;
            }
            else
            {
                lambdaBody = lambda.Body as ExpressionSyntax;
            }

            return lambdaBody;
        }
        private static string GetLambdaParameter(ExpressionSyntax expression)
        {
            var lambda = expression as SimpleLambdaExpressionSyntax;
            if (lambda != null)
            {
                return lambda.Parameter.Identifier.ValueText;
            }

            var parenthesizedLambda = expression as ParenthesizedLambdaExpressionSyntax;
            if (parenthesizedLambda == null ||
                !parenthesizedLambda.ParameterList.Parameters.Any())
            {
                return null;
            }
            return parenthesizedLambda.ParameterList.Parameters.First().Identifier.ValueText;
        }

        private static bool TryGetCastInLambda(SyntaxKind asOrIs, IMethodSymbol methodSymbol, InvocationExpressionSyntax invocation, out string type)
        {
            type = null;
            if (!AsIsSyntaxKinds.Contains(asOrIs))
            {
                return false;
            }

            var arguments = GetReducedArguments(methodSymbol, invocation);
            if (arguments.Count() != 1)
            {
                return false;
            }

            var expression = arguments.First().Expression;
            var lambdaBody = GetExpressionFromParens(GetExpressionFromLambda(expression)) as BinaryExpressionSyntax;
            var lambdaParameter = GetLambdaParameter(expression);
            if (lambdaBody == null ||
                lambdaParameter == null ||
                !lambdaBody.IsKind(asOrIs))
            {
                return false;
            }

            var castedExpression = GetExpressionFromParens(lambdaBody.Left);
            if (lambdaParameter != castedExpression.ToString())
            {
                return false;
            }

            type = lambdaBody.Right.ToString();
            return true;
        }
        private static bool TryGetCastInLambda(IMethodSymbol methodSymbol, InvocationExpressionSyntax invocation, out string type)
        {
            type = null;
            var arguments = GetReducedArguments(methodSymbol, invocation);
            if (arguments.Count() != 1)
            {
                return false;
            }

            var expression = arguments.First().Expression;
            var castExpression = GetExpressionFromParens(GetExpressionFromLambda(expression)) as CastExpressionSyntax;
            var lambdaParameter = GetLambdaParameter(expression);
            if (castExpression == null ||
                lambdaParameter == null)
            {
                return false;
            }

            var castedExpression = GetExpressionFromParens(castExpression.Expression);
            if (lambdaParameter != castedExpression.ToString())
            {
                return false;
            }

            type = castExpression.Type.ToString();
            return true;
        }

        private static bool CheckForSimplifiable(List<ArgumentSyntax> outerArguments, IMethodSymbol outerMethodSymbol,
            IMethodSymbol innerMethodSymbol, SyntaxNodeAnalysisContext c, InvocationExpressionSyntax innerInvocation)
        {
            if (!outerArguments.Any() &&
                MethodNamesWithPredicate.Contains(outerMethodSymbol.Name) &&
                innerMethodSymbol.Name == WhereMethodName &&
                innerMethodSymbol.Parameters.Any(symbol =>
                {
                    var namedType = symbol.Type as INamedTypeSymbol;
                    if (namedType == null)
                    {
                        return false;
                    }
                    return namedType.TypeArguments.Count() == 2;
                }))
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, GetReportLocation(innerInvocation),
                    string.Format(MessageFormatSimplify, WhereMethodName, outerMethodSymbol.Name)));
                return true;
            }
            return false;
        }
    }
}
