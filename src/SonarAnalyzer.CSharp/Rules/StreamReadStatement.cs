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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [Rule(DiagnosticId)]
    public class StreamReadStatement : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2674";
        internal const string MessageFormat =
            "Check the return value of the \"{0}\" call to see how many bytes were read.";

        private static readonly DiagnosticDescriptor rule =
            DiagnosticDescriptorBuilder.GetDescriptor(DiagnosticId, MessageFormat, RspecStrings.ResourceManager);

        protected sealed override DiagnosticDescriptor Rule => rule;

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var statement = (ExpressionStatementSyntax)c.Node;
                    var expression = statement.Expression;

                    var awaitExpression = expression as AwaitExpressionSyntax;
                    if (awaitExpression != null)
                    {
                        expression = awaitExpression.Expression;
                    }

                    var method = c.SemanticModel.GetSymbolInfo(expression).Symbol as IMethodSymbol;
                    if (method == null ||
                        !ReadMethodNames.Contains(method.Name, StringComparer.Ordinal))
                    {
                        return;
                    }

                    if (method.ContainingType.DerivesOrImplements(KnownType.System_IO_Stream))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation(), method.Name));
                    }
                },
                SyntaxKind.ExpressionStatement);
        }

        private static readonly string[] ReadMethodNames = { "Read", "ReadAsync" };
    }
}