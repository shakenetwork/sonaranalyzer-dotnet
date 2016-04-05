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
using System.Collections.Generic;
using System.Linq;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Cert, Tag.Misra)]
    public class ReturnValueIgnored : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2201";
        internal const string Title = "\"=+\" should not be used instead of \"+=\"";
        internal const string Description =
            "When the call to a function doesn't have any side effects, what is the point of making the call if the results " +
            "are ignored? In such case, either the function call is useless and should be dropped or the source code doesn't " +
            "behave as expected.";
        internal const string MessageFormat = "Use the return value of method \"{0}\", which has no side effect.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
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
                    var expressionStatement = (ExpressionStatementSyntax)c.Node;
                    CheckExpressionForPureMethod(c, expressionStatement.Expression);
                },
                SyntaxKind.ExpressionStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var lambda = (LambdaExpressionSyntax)c.Node;

                    var symbol = c.SemanticModel.GetSymbolInfo(lambda).Symbol as IMethodSymbol;
                    if (symbol == null ||
                        !symbol.ReturnsVoid)
                    {
                        return;
                    }

                    var expression = lambda.Body as ExpressionSyntax;
                    CheckExpressionForPureMethod(c, expression);
                },
                SyntaxKind.ParenthesizedLambdaExpression,
                SyntaxKind.SimpleLambdaExpression);
        }

        private static void CheckExpressionForPureMethod(SyntaxNodeAnalysisContext c, ExpressionSyntax expression)
        {
            var invocation = expression as InvocationExpressionSyntax;
            if (invocation == null)
            {
                return;
            }

            var invokedMethodSymbol = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (invokedMethodSymbol == null ||
                invokedMethodSymbol.ReturnsVoid)
            {
                return;
            }

            if (IsLinqMethod(invokedMethodSymbol) ||
                HasOnlySideEffectFreeMethods(invokedMethodSymbol.ContainingType) ||
                IsPureMethod(invokedMethodSymbol))
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation(), invokedMethodSymbol.Name));
            }
        }

        private static bool IsPureMethod(IMethodSymbol invokedMethodSymbol)
        {
            return !invokedMethodSymbol.ReturnsVoid &&
                invokedMethodSymbol.Parameters.All(p => p.RefKind == RefKind.None) &&
                invokedMethodSymbol.GetAttributes().Any(a => a.AttributeClass.Is(KnownType.System_Diagnostics_Contracts_PureAttribute));
        }

        private static bool HasOnlySideEffectFreeMethods(INamedTypeSymbol containingType)
        {
            return containingType.IsAny(ImmutableKnownTypes);
        }

        public static readonly ISet<KnownType> ImmutableKnownTypes = new HashSet<KnownType>(new[]
        {
            KnownType.System_Object,
            KnownType.System_Int16,
            KnownType.System_Int32,
            KnownType.System_Int64,
            KnownType.System_UInt16,
            KnownType.System_UInt32,
            KnownType.System_UInt64,
            KnownType.System_Char,
            KnownType.System_Byte,
            KnownType.System_SByte,
            KnownType.System_Single,
            KnownType.System_Double,
            KnownType.System_Decimal,
            KnownType.System_Boolean,
            KnownType.System_String,

            KnownType.System_Collections_Immutable_ImmutableArray,
            KnownType.System_Collections_Immutable_ImmutableArray_T,
            KnownType.System_Collections_Immutable_ImmutableDictionary,
            KnownType.System_Collections_Immutable_ImmutableDictionary_TKey_TValue,
            KnownType.System_Collections_Immutable_ImmutableHashSet,
            KnownType.System_Collections_Immutable_ImmutableHashSet_T,
            KnownType.System_Collections_Immutable_ImmutableList,
            KnownType.System_Collections_Immutable_ImmutableList_T,
            KnownType.System_Collections_Immutable_ImmutableQueue,
            KnownType.System_Collections_Immutable_ImmutableQueue_T,
            KnownType.System_Collections_Immutable_ImmutableSortedDictionary,
            KnownType.System_Collections_Immutable_ImmutableSortedDictionary_TKey_TValue,
            KnownType.System_Collections_Immutable_ImmutableSortedSet,
            KnownType.System_Collections_Immutable_ImmutableSortedSet_T,
            KnownType.System_Collections_Immutable_ImmutableStack,
            KnownType.System_Collections_Immutable_ImmutableStack_T
        });

        private static bool IsLinqMethod(IMethodSymbol methodSymbol)
        {
            return methodSymbol.IsExtensionOn(KnownType.System_Collections_Generic_IEnumerable_T) ||
                methodSymbol.IsExtensionOn(KnownType.System_Collections_IEnumerable);
        }
    }
}
