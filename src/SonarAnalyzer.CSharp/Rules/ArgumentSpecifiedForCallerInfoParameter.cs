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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Linq;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious)]
    public class ArgumentSpecifiedForCallerInfoParameter : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3236";
        internal const string Title = "Methods with caller info attributes should not be invoked with explicit arguments";
        internal const string Description =
            "Caller information attributes (\"CallerFilePathAttribute\", \"CallerLineNumberAttribute\", and \"CallerMemberNameAttribute\") " +
            "provide a way to get information about the caller of a method through optional parameters. But the arguments for these " +
            "optional parameters are only generated if they are not explicitly defined in the call. Thus, specifying the argument values " +
            "defeats the purpose of the attributes.";
        internal const string MessageFormat = "Remove this argument from the method call; it hides the caller information.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
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
                    var methodCall = (InvocationExpressionSyntax)c.Node;
                    var methodParameterLookup = new MethodParameterLookup(methodCall, c.SemanticModel);
                    var argumentMappings = methodParameterLookup.GetAllArgumentParameterMappings()
                        .ToList();

                    var methodSymbol = methodParameterLookup.MethodSymbol;
                    if (methodSymbol == null)
                    {
                        return;
                    }

                    foreach (var argumentMapping in argumentMappings)
                    {
                        var parameter = argumentMapping.Parameter;
                        var argument = argumentMapping.Argument;

                        var callerInfoAttributeDataOnCall = GetCallerInfoAttribute(parameter);
                        if (callerInfoAttributeDataOnCall == null)
                        {
                            continue;
                        }

                        var symbolForArgument = c.SemanticModel.GetSymbolInfo(argument.Expression).Symbol as IParameterSymbol;
                        if (symbolForArgument != null &&
                            object.Equals(callerInfoAttributeDataOnCall.AttributeClass, GetCallerInfoAttribute(symbolForArgument)?.AttributeClass))
                        {
                            continue;
                        }

                        c.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation()));
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static AttributeData GetCallerInfoAttribute(IParameterSymbol parameter)
        {
            return parameter.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass.IsAny(KnownType.CallerInfoAttributes));
        }
    }
}
