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
using System.Globalization;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Confusing)]
    public class StringFormatWithNoArgument : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3457";
        internal const string Title = "\"string.Format()\" should not be called without placeholders";
        internal const string Description =
            "There's no need to incur the overhead of a formatting call when the string to be formatted contains no formatting " +
            "symbols. Instead, simply use the original input string.";
        internal const string MessageFormat = "Remove this formatting call and simply use the input string.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        internal const string FormatStringIndexKey = "formatStringIndex";

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

                    if (methodSymbol == null ||
                        methodSymbol.ContainingType.SpecialType != SpecialType.System_String ||
                        methodSymbol.Name != "Format")
                    {
                        return;
                    }

                    if (invocation.ArgumentList == null ||
                        invocation.ArgumentList.Arguments.Count == 0)
                    {
                        return;
                    }

                    var lookup = new MethodParameterLookup(invocation, c.SemanticModel);
                    if (!InvocationHasFormatArgument(invocation, lookup))
                    {
                        var formatArgument = invocation.ArgumentList.Arguments
                            .FirstOrDefault(arg => lookup.GetParameterSymbol(arg).Name == "format");
                        if (formatArgument == null)
                        {
                            return;
                        }

                        var constValue = c.SemanticModel.GetConstantValue(formatArgument.Expression);
                        if (!constValue.HasValue)
                        {
                            // we don't report on non-contant format strings
                            return;
                        }

                        var formatString = constValue.Value as string;
                        if (formatString == null)
                        {
                            return;
                        }

                        if (!StringFormatArgumentNumberMismatch.FormatterAcceptsArgumentCount(formatString, 0))
                        {
                            ///A more severe issue is already reported by <see cref="StringFormatArgumentNumberMismatch"/>
                            return;
                        }

                        c.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Expression.GetLocation(),
                            ImmutableDictionary<string, string>.Empty.Add(
                                FormatStringIndexKey,
                                invocation.ArgumentList.Arguments.IndexOf(formatArgument).ToString(CultureInfo.InvariantCulture))));
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static bool InvocationHasFormatArgument(InvocationExpressionSyntax invocation, MethodParameterLookup lookup)
        {
            return invocation.ArgumentList.Arguments.Any(arg =>
            {
                var param = lookup.GetParameterSymbol(arg);
                return param != null && param.Name.StartsWith("arg", System.StringComparison.Ordinal);
            });
        }
    }
}
