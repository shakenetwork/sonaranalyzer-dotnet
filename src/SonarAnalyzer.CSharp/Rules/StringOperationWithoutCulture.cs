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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cert, Tag.Unpredictable)]
    public class StringOperationWithoutCulture : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1449";
        internal const string Title = "Culture should be specified for \"string\" operations";
        internal const string Description =
            "\"string.ToLower()\", \"ToUpper\", \"IndexOf\", \"LastIndexOf\", and \"Compare\" are all culture-dependent, " +
            "as are some (floating point number and \"DateTime\"-related) calls to \"ToString\". Fortunately, all have variants which accept an argument " +
            "specifying the culture or formatter to use. Leave that argument off and the call will use the system default culture, " +
            "possibly creating problems with international characters. \"string.CompareTo()\" is also culture specific, but has no overload that " +
            "takes a culture information, so instead it's better to use \"CompareOrdinal\", or \"Compare\" with culture. Calls without a " +
            "culture may work fine in the system's \"home\" environment, but break in ways that are extremely difficult to diagnose for customers " +
            "who use different encodings. Such bugs can be nearly, if not completely, impossible to reproduce when it's time to fix them.";
        internal const string MessageDefineLocale = "Define the locale to be used in this string operation.";
        internal const string MessageChangeCompareTo = "Use \"CompareOrdinal\" or \"Compare\" with the locale specified instead of \"CompareTo\".";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, "{0}", Category,
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

                    if (calledMethod.IsInType(KnownType.System_String)  &&
                        CommonCultureSpecificMethodNames.Contains(calledMethod.Name) &&
                        !calledMethod.Parameters
                            .Any(param => param.Type.IsAny(StringCultureSpecifierNames)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), MessageDefineLocale));
                        return;
                    }

                    if (calledMethod.IsInType(KnownType.System_String) &&
                        IndexLookupMethodNames.Contains(calledMethod.Name) &&
                        calledMethod.Parameters.Any(param => param.Type.SpecialType == SpecialType.System_String) &&
                        !calledMethod.Parameters.Any(param => param.Type.IsAny(StringCultureSpecifierNames)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), MessageDefineLocale));
                        return;
                    }

                    if (IsMethodOnNonIntegralOrDateTime(calledMethod) &&
                        calledMethod.Name == ToStringMethodName &&
                        !calledMethod.Parameters.Any())
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), MessageDefineLocale));
                        return;
                    }

                    if (calledMethod.IsInType(KnownType.System_String) &&
                        calledMethod.Name == CompareToMethodName)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), MessageChangeCompareTo));
                        return;
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static bool IsMethodOnNonIntegralOrDateTime(IMethodSymbol methodSymbol)
        {
            return methodSymbol.IsInType(KnownType.NonIntegralNumbers) ||
                methodSymbol.IsInType(KnownType.System_DateTime);
        }

        private static readonly ISet<string> CommonCultureSpecificMethodNames = ImmutableHashSet.Create(
            "ToLower",
            "ToUpper",
            "Compare");

        private static readonly ISet<string> IndexLookupMethodNames = ImmutableHashSet.Create(
            "IndexOf",
            "LastIndexOf");

        private const string CompareToMethodName = "CompareTo";
        private const string ToStringMethodName = "ToString";

        private static readonly ISet<KnownType> StringCultureSpecifierNames = ImmutableHashSet.Create(
            KnownType.System_Globalization_CultureInfo,
            KnownType.System_Globalization_CompareOptions,
            KnownType.System_StringComparison);
    }
}
