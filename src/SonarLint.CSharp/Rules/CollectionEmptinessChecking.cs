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
using System.Linq;
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
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class CollectionEmptinessChecking : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1155";
        internal const string Title = "\"Any()\" should be used to test for emptiness";
        internal const string Description =
            "Using \".Count() > 0\" to test for emptiness works, but using \".Any()\" makes the " +
            "intent clearer, and the code more readable.";
        internal const string MessageFormat = "Use \".Any()\" to test whether this \"IEnumerable<{0}>\" is empty or not.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binary = (BinaryExpressionSyntax) c.Node;
                    CheckCountZero(binary.Right, binary.Left, c);
                    CheckCountOne(binary.Left, binary.Right, c);
                },
                SyntaxKind.GreaterThanExpression);
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binary = (BinaryExpressionSyntax)c.Node;
                    CheckCountZero(binary.Left, binary.Right, c);
                    CheckCountOne(binary.Right, binary.Left, c);
                },
                SyntaxKind.LessThanExpression);
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binary = (BinaryExpressionSyntax)c.Node;
                    CheckCountOne(binary.Right, binary.Left, c);
                    CheckCountZero(binary.Left, binary.Right, c);
                },
                SyntaxKind.GreaterThanOrEqualExpression);
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binary = (BinaryExpressionSyntax)c.Node;
                    CheckCountOne(binary.Left, binary.Right, c);
                    CheckCountZero(binary.Right, binary.Left, c);
                },
                SyntaxKind.LessThanOrEqualExpression);
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binary = (BinaryExpressionSyntax)c.Node;
                    CheckCountZero(binary.Left, binary.Right, c);
                    CheckCountZero(binary.Right, binary.Left, c);
                },
                SyntaxKind.EqualsExpression);
        }

        private static void CheckCountZero(ExpressionSyntax zero, ExpressionSyntax count, SyntaxNodeAnalysisContext context)
        {
            Location reportLocation;
            string typeArgument;
            int value;
            if (ExpressionNumericConverter.TryGetConstantIntValue(zero, out value) &&
                value == 0 &&
                TryGetCountCall(count, context.SemanticModel, out reportLocation, out typeArgument))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, reportLocation, typeArgument));
            }
        }
        private static void CheckCountOne(ExpressionSyntax one, ExpressionSyntax count, SyntaxNodeAnalysisContext context)
        {
            Location reportLocation;
            string typeArgument;
            int value;
            if (ExpressionNumericConverter.TryGetConstantIntValue(one, out value) &&
                value == 1 &&
                TryGetCountCall(count, context.SemanticModel, out reportLocation, out typeArgument))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, reportLocation, typeArgument));
            }
        }

        private static bool TryGetCountCall(ExpressionSyntax expression, SemanticModel semanticModel, out Location countLocation, out string typeArgument)
        {
            countLocation = null;
            typeArgument = null;
            var invocation = expression as InvocationExpressionSyntax;
            if (invocation == null)
            {
                return false;
            }

            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
            {
                return false;
            }

            var methodSymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol as IMethodSymbol;
            if (!IsMethodCountExtension(methodSymbol) ||
                !MethodIsOnGenericIEnumerable(methodSymbol))
            {
                return false;
            }

            if (methodSymbol.IsGenericMethod)
            {
                typeArgument = methodSymbol.TypeArguments.First().ToDisplayString();
            }

            countLocation = memberAccess.Name.GetLocation();
            return true;
        }

        private static bool IsMethodCountExtension(IMethodSymbol methodSymbol)
        {
            return methodSymbol != null &&
                methodSymbol.Name == "Count" &&
                methodSymbol.IsExtensionMethod &&
                methodSymbol.ReceiverType != null;
        }

        internal static bool MethodIsOnGenericIEnumerable(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
            {
                return false;
            }

            var receiverType = methodSymbol.ReceiverType as INamedTypeSymbol;

            if (methodSymbol.MethodKind == MethodKind.Ordinary &&
                methodSymbol.IsExtensionMethod)
            {
                receiverType = methodSymbol.Parameters.First().Type as INamedTypeSymbol;
            }

            var constructedFrom = receiverType?.ConstructedFrom;
            return constructedFrom.Is(KnownType.System_Collections_Generic_IEnumerable_T);
        }
    }
}
