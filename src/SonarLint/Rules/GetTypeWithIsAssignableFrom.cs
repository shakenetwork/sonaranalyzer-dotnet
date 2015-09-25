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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class GetTypeWithIsAssignableFrom : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2219";
        internal const string Title = "\"Type.IsAssignableFrom()\" should not be used to check object type";
        internal const string Description =
            "To check the type of an object there are at least three options: the simplest and shortest one uses the \"expr is SomeType\" " +
            "operator, the slightly longer \"typeInstance.IsInstanceOfType(expr)\", and the cumbersome and error-prone one uses " +
            "\"expr1.GetType().IsAssignableFrom(expr2.GetType())\"";
        internal const string MessageFormat = "Use the {0} instead.";
        internal const string IsOperator = "\"is\" operator";
        internal const string IsInstanceOfType = "\"IsInstanceOfType()\" method";
        internal const string Category = "SonarLint";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        internal const string UseIsOperatorKey = "UseIsOperator";

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
                        methodSymbol.Name != "IsAssignableFrom" ||
                        invocation.ArgumentList.Arguments.Count != 1)
                    {
                        return;
                    }

                    var argument = invocation.ArgumentList.Arguments.First().Expression;
                    if (IsGetTypeCall(argument, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                            ImmutableDictionary<string, string>.Empty.Add(UseIsOperatorKey, false.ToString()),
                            IsInstanceOfType));
                        return;
                    }

                    if (IsInvocationOnGetTypeAndTypeOfArgument(memberAccess.Expression, argument, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                            ImmutableDictionary<string, string>.Empty.Add(UseIsOperatorKey, true.ToString()),
                            IsOperator));
                        return;
                    }
                },
                SyntaxKind.InvocationExpression);
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

        private static bool IsInvocationOnGetTypeAndTypeOfArgument(ExpressionSyntax expression, ExpressionSyntax argument,
            SemanticModel semanticModel)
        {
            return argument is TypeOfExpressionSyntax &&
                IsGetTypeCall(expression, semanticModel);
        }
    }
}
