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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [SqaleConstantRemediation("15min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.ErrorHandling, Tag.Security)]
    public class EnumerableSumInUnchecked : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2291";
        internal const string Title = "Overflow checking should not be disabled for \"Enumerable.Sum\"";
        internal const string Description =
            "\"Enumerable.Sum()\" always executes addition in a \"checked\" context, so an \"OverflowException\" will " +
            "be thrown if the value exceeds \"MaxValue\" even if an \"unchecked\" context was specified. Using an " +
            "\"unchecked\" context anyway represents a misunderstanding of how \"Sum\" works.";
        internal const string MessageFormat = "Refactor this code to handle \"OverflowException\".";
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
                    var invocation = (InvocationExpressionSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

                    if (IsSumOnInteger(methodSymbol) &&
                        IsSumInsideUnchecked(invocation))
                    {
                        var expression = invocation.Expression;
                        var memberAccess = expression as MemberAccessExpressionSyntax;
                        if (memberAccess == null)
                        {
                            return;
                        }
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation()));
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static bool IsSumInsideUnchecked(InvocationExpressionSyntax invocation)
        {
            SyntaxNode current = invocation;
            SyntaxNode parent = current.Parent;
            while (parent != null)
            {
                var tryStatement = parent as TryStatementSyntax;
                if (tryStatement != null &&
                    tryStatement.Block == current)
                {
                    return false;
                }

                if (IsUncheckedExpression(parent) ||
                    IsUncheckedStatement(parent))
                {
                    return true;
                }

                current = parent;
                parent = parent.Parent;
            }
            return false;
        }

        private static bool IsUncheckedExpression(SyntaxNode node)
        {
            var uncheckedExpression = node as CheckedExpressionSyntax;
            return uncheckedExpression != null &&
                uncheckedExpression.IsKind(SyntaxKind.UncheckedExpression);
        }

        private static bool IsUncheckedStatement(SyntaxNode node)
        {
            var uncheckedExpression = node as CheckedStatementSyntax;
            return uncheckedExpression != null &&
                uncheckedExpression.IsKind(SyntaxKind.UncheckedStatement);
        }

        private static bool IsSumOnInteger(IMethodSymbol methodSymbol)
        {
            return methodSymbol != null &&
                CollectionEmptinessChecking.MethodIsOnGenericIEnumerable(methodSymbol) &&
                methodSymbol.Name == "Sum" &&
                IsReturnTypeCandidate(methodSymbol);
        }

        private static bool IsReturnTypeCandidate(IMethodSymbol methodSymbol)
        {
            var returnType = methodSymbol.ReturnType;
            if (returnType.OriginalDefinition.Is(KnownType.System_Nullable_T))
            {
                var nullableType = (INamedTypeSymbol)returnType;
                if (nullableType.TypeArguments.Length != 1)
                {
                    return false;
                }
                returnType = nullableType.TypeArguments[0];
            }

            return returnType.IsAny(DisallowedTypes);
        }

        private static readonly ISet<KnownType> DisallowedTypes = new []
        {
            KnownType.System_Int64,
            KnownType.System_Int32,
            KnownType.System_Decimal,
        }.ToImmutableHashSet();
    }
}
