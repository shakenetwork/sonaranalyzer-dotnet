/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Unpredictable)]
    public class StringOperationWithoutCulture : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1449";
        internal const string Title = "Culture should be specified for String operations";
        internal const string Description =
            "\"String.ToLower()\", \".ToUpper\", \".Compare\", and \".Equals\" are all culture-dependent, " +
            "as are some (floating point number-related) calls to \"ToString\". Fortunately, all have variants which accept an argument " +
            "specifying the culture or formatter to use. Leave that argument off and the call will use the system default culture, " +
            "possibly creating problems with international characters. Such calls without a culture may work fine in the system's \"home\" " +
            "environment, but break in ways that are extremely difficult to diagnose for customers who use different encodings. Such bugs " +
            "can be nearly, if not completely, impossible to reproduce when it's time to fix them.";
        internal const string MessageFormat = "Define the locale to be used in this String operation.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

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
                    var invocation = (InvocationExpressionSyntax)c.Node;
                    var calledMethod = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (calledMethod == null)
                    {
                        return;
                    }

                    var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
                    if(memberAccess == null)
                    {
                        return;
                    }

                    if (calledMethod.ContainingType.SpecialType == SpecialType.System_String &&
                        ZeroParameterMethods.Contains(calledMethod.Name) &&
                        !calledMethod.Parameters.Any())
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation()));
                        return;
                    }

                    if (calledMethod.ContainingType.SpecialType == SpecialType.System_String &&
                        calledMethod.Name == EqualsMethodName &&
                        calledMethod.Parameters.Count() == 1 &&
                        calledMethod.Parameters.First().Type.SpecialType == SpecialType.System_String)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation()));
                        return;
                    }

                    var cultureInfoType = c.SemanticModel.Compilation.GetTypeByMetadataName("System.Globalization.CultureInfo");
                    var compareOptionsType = c.SemanticModel.Compilation.GetTypeByMetadataName("System.Globalization.CompareOptions");

                    if (cultureInfoType == null || compareOptionsType == null)
                    {
                        return;
                    }

                    if (calledMethod.ContainingType.SpecialType == SpecialType.System_String &&
                        calledMethod.Name == CompareMethodName &&
                        !calledMethod.Parameters
                            .Any(param => param.Type.Equals(cultureInfoType) || param.Type.Equals(compareOptionsType)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation()));
                        return;
                    }

                    var iFormatProviderType = c.SemanticModel.Compilation.GetTypeByMetadataName("System.IFormatProvider");
                    if (iFormatProviderType == null)
                    {
                        return;
                    }

                    if (IsMethodOnNonIntegerNumeric(calledMethod) &&
                        calledMethod.Name == ToStringMethodName &&
                        !calledMethod.Parameters
                            .Any(param => param.Type.Equals(iFormatProviderType)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation()));
                        return;
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static bool IsMethodOnNonIntegerNumeric(IMethodSymbol methodSymbol)
        {
            return methodSymbol.ContainingType.SpecialType == SpecialType.System_Single ||
                methodSymbol.ContainingType.SpecialType == SpecialType.System_Double ||
                methodSymbol.ContainingType.SpecialType == SpecialType.System_Decimal;
        }

        private static readonly string[] ZeroParameterMethods = { ToLowerMethodName, ToUpperMethodName };
        private const string ToLowerMethodName = "ToLower";
        private const string ToUpperMethodName = "ToUpper";
        private const string EqualsMethodName = "Equals";
        private const string CompareMethodName = "Compare";
        private const string ToStringMethodName = "ToString";
    }
}
