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
using System;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [SqaleConstantRemediation("15min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class StreamReadStatement : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2674";
        internal const string Title = "The length returned from a stream read should be checked";
        internal const string Description =
            "You cannot assume that any given stream reading call will fill the \"byte[]\" passed in to the method with " +
            "the number of bytes requested. Instead, you must check the value returned by the read method to see how " +
            "many bytes were read. Fail to do so, and you introduce a bug that is both harmful and difficult to " +
            "reproduce.";
        internal const string MessageFormat =
            "Check the return value of the \"{0}\" call to see how many bytes were read.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Blocker;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
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
                        !ReadMethodNames.Contains(method.Name, StringComparer.InvariantCulture))
                    {
                        return;
                    }

                    var streamType = c.SemanticModel.Compilation.GetTypeByMetadataName("System.IO.Stream");
                    if (streamType == null)
                    {
                        return;
                    }

                    if (method.ContainingType.DerivesOrImplementsAny(streamType))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation(), method.Name));
                    }
                },
                SyntaxKind.ExpressionStatement);
        }

        private static readonly string[] ReadMethodNames = { "Read", "ReadAsync" };
    }
}