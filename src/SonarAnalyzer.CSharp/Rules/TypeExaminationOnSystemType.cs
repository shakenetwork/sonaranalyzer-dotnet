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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using Microsoft.CodeAnalysis.Text;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious)]
    public class TypeExaminationOnSystemType : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3443";
        internal const string Title = "Type examining methods should be avoided on \"System.Type\" instances";
        internal const string Description =
            "If you call \"GetType()\" on a \"Type\" variable, the return value will always be \"typeof(System.Type)\". So there's no real point in making " +
            "that call. The same applies to passing a type argument to \"IsInstanceOfType\". In both cases the results are entirely predictable.";
        internal const string MessageGetType = "Remove this use of \"GetType\" on a \"System.Type\".";
        internal const string MessageIsInstanceOfType = "Pass an argument that is not a \"System.Type\" or consider using \"IsAssignableFrom\".";
        internal const string MessageIsInstanceOfTypeWithGetType = "Consider removing the \"GetType\" call, it's suspicious in an \"IsInstanceOfType\" call.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, "{0}", Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax)c.Node;

                    var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (methodSymbol == null)
                    {
                        return;
                    }

                    CheckGetTypeCallOnType(invocation, methodSymbol, c);
                    CheckIsInstanceOfTypeCallWithTypeArgument(invocation, methodSymbol, c);
                },
                SyntaxKind.InvocationExpression);
        }

        private static void CheckIsInstanceOfTypeCallWithTypeArgument(InvocationExpressionSyntax invocation, IMethodSymbol methodSymbol,
            SyntaxNodeAnalysisContext context)
        {
            if (methodSymbol.Name != "IsInstanceOfType" ||
                !methodSymbol.ContainingType.Is(KnownType.System_Type) ||
                !invocation.HasExactlyNArguments(1))
            {
                return;
            }

            var argument = invocation.ArgumentList.Arguments.First().Expression;

            var typeInfo = context.SemanticModel.GetTypeInfo(argument).Type;
            if (!typeInfo.Is(KnownType.System_Type))
            {
                return;
            }

            var invocationInArgument = argument as InvocationExpressionSyntax;
            var message = IsGetTypeCall(invocationInArgument, context.SemanticModel)
                ? MessageIsInstanceOfTypeWithGetType
                : MessageIsInstanceOfType;

            context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation(), message));
        }

        private static void CheckGetTypeCallOnType(InvocationExpressionSyntax invocation, IMethodSymbol invokedMethod,
            SyntaxNodeAnalysisContext context)
        {
            var memberCall = invocation.Expression as MemberAccessExpressionSyntax;

            if (memberCall == null ||
                !IsGetTypeCall(invokedMethod))
            {
                return;
            }

            var expressionType = context.SemanticModel.GetTypeInfo(memberCall.Expression).Type;
            if (!expressionType.Is(KnownType.System_Type))
            {
                return;
            }

            var location = Location.Create(memberCall.SyntaxTree, TextSpan.FromBounds(memberCall.OperatorToken.SpanStart, invocation.Span.End));
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, MessageGetType));
        }

        private static bool IsGetTypeCall(IMethodSymbol invokedMethod)
        {
            return invokedMethod.Name == "GetType" &&
                !invokedMethod.IsStatic &&
                invokedMethod.ContainingType != null &&
                IsObjectOrType(invokedMethod.ContainingType);
        }

        private static bool IsObjectOrType(INamedTypeSymbol namedType)
        {
            return namedType.SpecialType == SpecialType.System_Object ||
                namedType.Is(KnownType.System_Type);
        }

        internal static bool IsGetTypeCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            if (invocation == null)
            {
                return false;
            }

            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            return methodSymbol != null && IsGetTypeCall(methodSymbol);
        }
    }
}
