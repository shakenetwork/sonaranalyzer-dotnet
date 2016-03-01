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

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class OptionalParameterNotPassedToBaseCall : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3466";
        internal const string Title = "Optional parameters should be passed to \"base\" calls";
        internal const string Description =
            "Generally, writing the least code that will readably do the job is a good thing, so omitting default parameter values " +
            "seems to make sense. Unfortunately, when you omit them from the \"base\" call in an override, you're not actually " +
            "getting the job done thoroughly, because you're ignoring the value the caller passed in. The result will likely not be " +
            "what the caller expected.";
        internal const string MessageFormat = "Pass the missing user-supplied parameter value{0} to this \"base\" call.";
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
                    if (!IsOnBase(invocation) ||
                        invocation.ArgumentList == null)
                    {
                        return;
                    }

                    var calledMethod = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (calledMethod == null ||
                        !calledMethod.IsVirtual ||
                        invocation.ArgumentList.Arguments.Count == calledMethod.Parameters.Length ||
                        !IsCallInsideOverride(invocation, calledMethod, c.SemanticModel))
                    {
                        return;
                    }

                    var pluralize = calledMethod.Parameters.Length - invocation.ArgumentList.Arguments.Count > 1
                        ? "s"
                        : string.Empty;
                    c.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), pluralize));
                },
                SyntaxKind.InvocationExpression);
        }

        private static bool IsCallInsideOverride(InvocationExpressionSyntax invocation, IMethodSymbol calledMethod, 
            SemanticModel semanticModel)
        {
            var enclosingSymbol = semanticModel.GetEnclosingSymbol(invocation.SpanStart) as IMethodSymbol;

            return enclosingSymbol != null && 
                enclosingSymbol.IsOverride &&
                object.Equals(enclosingSymbol.OverriddenMethod, calledMethod);
        }

        private static bool IsOnBase(InvocationExpressionSyntax invocation)
        {
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            return memberAccess != null &&
                memberAccess.Expression.IsKind(SyntaxKind.BaseExpression);
        }
    }
}
