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
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class ReferenceEqualsOnValueType : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2995";
        internal const string Title = "\"Object.ReferenceEquals\" should not be used for value types";
        internal const string Description =
            "Using \"Object.ReferenceEquals\" to compare the references of two value types simply won't return the " +
            "expected results most of the time because such types are passed by value, not by reference.";
        internal const string MessageFormat = "Use a different kind of comparison for these value types.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        private const string ReferenceEqualsName = "ReferenceEquals";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax) c.Node;

                    var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (methodSymbol.IsInType(KnownType.System_Object) &&
                        methodSymbol.Name == ReferenceEqualsName &&
                        AnyArgumentIsValueType(invocation.ArgumentList, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Expression.GetLocation()));
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static bool AnyArgumentIsValueType(ArgumentListSyntax argumentList, SemanticModel semanticModel)
        {
            return argumentList.Arguments.Any(argument =>
            {
                var type = semanticModel.GetTypeInfo(argument.Expression).Type;
                return type != null && type.IsValueType;
            });
        }
    }
}
