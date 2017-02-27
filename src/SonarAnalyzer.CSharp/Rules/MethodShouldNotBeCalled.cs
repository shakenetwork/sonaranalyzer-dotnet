/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2017 SonarSource SA
 * mailto: contact AT sonarsource DOT com
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
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules
{
    public abstract class MethodShouldNotBeCalled : SonarDiagnosticAnalyzer
    {
        protected class MethodSignature
        {
            internal MethodSignature(KnownType namespaceName, string methodName)
            {
                Namespace = namespaceName;
                MethodName = methodName;
            }

            internal KnownType Namespace { get; }
            internal string MethodName { get; }
        }

        protected const string MessageFormat = "Refactor the code to remove this use of '{0}'";

        protected abstract IEnumerable<MethodSignature> InvalidMethods { get; }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(CheckForIssue, SyntaxKind.InvocationExpression);
        }

        private void CheckForIssue(SyntaxNodeAnalysisContext analysisContext)
        {
            var invocation = (InvocationExpressionSyntax)analysisContext.Node;
            var call = invocation.Expression as MemberAccessExpressionSyntax;
            if (call == null)
            {
                return;
            }

            var methodName = InvalidMethods.FirstOrDefault(method =>
                method.MethodName.Equals(call.Name.Identifier.ValueText));
            if (methodName == null)
            {
                return;
            }

            var methodCallSymbol = analysisContext.SemanticModel.GetSymbolInfo(call.Name);
            if (methodCallSymbol.Symbol == null)
            {
                return;
            }

            if (!methodCallSymbol.Symbol.ContainingType.ConstructedFrom.Is(methodName.Namespace))
            {
                return;
            }

            analysisContext.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                methodName.MethodName));
        }
    }
}
