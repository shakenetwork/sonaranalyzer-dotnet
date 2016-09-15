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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class ParametersCorrectOrder : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2234";
        internal const string Title = "Parameters should be passed in the correct order";
        internal const string Description =
            "When the names of parameters in a method call match the names of the method arguments, " +
            "it contributes to clearer, more readable code. However, when the names match, but are " +
            "passed in a different order than the method arguments, it indicates a mistake in the " +
            "parameter order which will likely lead to unexpected results.";
        internal const string MessageFormat = "Parameters to \"{0}\" have the same names but not the same order as the method arguments.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Blocker;
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
                    var methodCall = (InvocationExpressionSyntax) c.Node;
                    var methodParameterLookup = new MethodParameterLookup(methodCall, c.SemanticModel);
                    var argumentParameterMappings = methodParameterLookup.GetAllArgumentParameterMappings()
                        .ToDictionary(pair => pair.Argument, pair => pair.Parameter);

                    var methodSymbol = methodParameterLookup.MethodSymbol;
                    if (methodSymbol == null)
                    {
                        return;
                    }

                    var parameterNames = argumentParameterMappings.Values
                        .Select(symbol => symbol.Name)
                        .Distinct()
                        .ToList();

                    var identifierArguments = GetIdentifierArguments(methodCall);
                    var identifierNames = identifierArguments
                        .Select(p => p.IdentifierName)
                        .ToList();

                    if (!parameterNames.Intersect(identifierNames).Any())
                    {
                        return;
                    }

                    var methodCallHasIssue = false;

                    for (var i = 0; !methodCallHasIssue && i < identifierArguments.Count; i++)
                    {
                        var identifierArgument = identifierArguments[i];
                        var identifierName = identifierArgument.IdentifierName;
                        var parameter = argumentParameterMappings[identifierArgument.ArgumentSyntax];
                        var parameterName = parameter.Name;

                        if (string.IsNullOrEmpty(identifierName) ||
                            !parameterNames.Contains(identifierName))
                        {
                            continue;
                        }

                        var positional = identifierArgument as PositionalIdentifierArgument;
                        if (positional != null &&
                            (parameter.IsParams ||
                             !identifierNames.Contains(parameterName) ||
                             identifierName == parameterName))
                        {
                            continue;
                        }

                        var named = identifierArgument as NamedIdentifierArgument;
                        if (named != null &&
                            (!identifierNames.Contains(named.DeclaredName) || named.DeclaredName == named.IdentifierName))
                        {
                            continue;
                        }

                        methodCallHasIssue = true;
                    }

                    if (methodCallHasIssue)
                    {
                        var memberAccess = methodCall.Expression as MemberAccessExpressionSyntax;
                        var reportLocation = memberAccess == null
                            ? methodCall.Expression.GetLocation()
                            : memberAccess.Name.GetLocation();

                        c.ReportDiagnostic(Diagnostic.Create(Rule, reportLocation,
                            methodSymbol.Name));
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static List<IdentifierArgument> GetIdentifierArguments(InvocationExpressionSyntax methodCall)
        {
            return methodCall.ArgumentList.Arguments
                .Select((argument, index) =>
                {
                    var identifier = argument.Expression as IdentifierNameSyntax;
                    var identifierName = identifier?.Identifier.Text;

                    IdentifierArgument identifierArgument;
                    if (argument.NameColon == null)
                    {
                        identifierArgument = new PositionalIdentifierArgument
                        {
                            IdentifierName = identifierName,
                            Position = index,
                            ArgumentSyntax = argument
                        };
                    }
                    else
                    {
                        identifierArgument = new NamedIdentifierArgument
                        {
                            IdentifierName = identifierName,
                            DeclaredName = argument.NameColon.Name.Identifier.Text,
                            ArgumentSyntax = argument
                        };
                    }
                    return identifierArgument;
                })
                .ToList();
        }

        internal class IdentifierArgument
        {
            public string IdentifierName { get; set; }
            public ArgumentSyntax ArgumentSyntax { get; set; }
        }
        internal class PositionalIdentifierArgument : IdentifierArgument
        {
            public int Position { get; set; }
        }
        internal class NamedIdentifierArgument : IdentifierArgument
        {
            public string DeclaredName { get; set; }
        }
    }
}
