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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall, Tag.Unused)]
    public class MethodOverloadOptionalParameter : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3427";
        internal const string Title = "Method overloads with default parameter values should not overlap";
        internal const string Description =
            "The rules for method resolution are complex and perhaps not properly understood by all coders. Having " +
            "overloads with optional parameter values make the matter even harder to understand. An overload with default " +
            "parameter values can be hidden by one without the optional parameters.";
        internal const string MessageFormat =
            "This method signature overlaps the one defined on line {0}{1}, the default parameter value {2}.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private class ParameterHidingMethodInfo
        {
            public IParameterSymbol ParameterToReportOn { get; set; }
            public IMethodSymbol HidingMethod { get; set; }
            public IMethodSymbol HiddenMethod { get; set; }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var methodSymbol = c.Symbol as IMethodSymbol;
                    if (methodSymbol == null ||
                        methodSymbol.IsInterfaceImplementationOrMemberOverride() ||
                        !methodSymbol.Parameters.Any(p => p.IsOptional))
                    {
                        return;
                    }

                    var methods = methodSymbol.ContainingType.GetMembers(methodSymbol.Name)
                        .OfType<IMethodSymbol>();

                    var hidingInfos = new List<ParameterHidingMethodInfo>();

                    var matchingNamedMethods = methods
                        .Where(m => m.Parameters.Length < methodSymbol.Parameters.Length)
                        .Where(m => !m.Parameters.Any(p => p.IsParams));

                    foreach (var candidateHidingMethod in matchingNamedMethods
                        .Where(candidateHidingMethod => IsMethodHidingOriginal(candidateHidingMethod, methodSymbol))
                        .Where(candidateHidingMethod => methodSymbol.Parameters[candidateHidingMethod.Parameters.Length].IsOptional))
                    {
                        hidingInfos.Add(
                            new ParameterHidingMethodInfo
                            {
                                ParameterToReportOn = methodSymbol.Parameters[candidateHidingMethod.Parameters.Length],
                                HiddenMethod = methodSymbol,
                                HidingMethod = candidateHidingMethod
                            });
                    }

                    foreach (var hidingInfo in hidingInfos)
                    {
                        var syntax = hidingInfo.ParameterToReportOn.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                        if (syntax == null)
                        {
                            continue;
                        }

                        var hidingMethod = hidingInfo.HidingMethod;
                        if (hidingMethod.PartialImplementationPart != null)
                        {
                            hidingMethod = hidingMethod.PartialImplementationPart;
                        }

                        var hidingMethodSyntax = hidingMethod.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                        if (hidingMethodSyntax == null)
                        {
                            continue;
                        }

                        var defaultCanBeUsed =
                            IsMoreParameterAvailableInConflicting(hidingInfo) ||
                            !MethodsUsingSameParameterNames(hidingInfo);

                        var isOtherFile = syntax.SyntaxTree.FilePath != hidingMethodSyntax.SyntaxTree.FilePath;

                        c.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule, syntax.GetLocation(),
                            hidingMethodSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            isOtherFile
                                ? $" in file \"{new FileInfo(hidingMethodSyntax.SyntaxTree.FilePath).Name}\""
                                : string.Empty,
                            defaultCanBeUsed
                                ? "can only be used with named arguments"
                                : "can't be used"));
                    }
                },
                SymbolKind.Method);
        }

        private static bool MethodsUsingSameParameterNames(ParameterHidingMethodInfo hidingInfo)
        {
            for (int i = 0; i < hidingInfo.HidingMethod.Parameters.Length; i++)
            {
                if (hidingInfo.HidingMethod.Parameters[i].Name !=
                    hidingInfo.HiddenMethod.Parameters[i].Name)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsMoreParameterAvailableInConflicting(ParameterHidingMethodInfo hidingInfo)
        {
            return hidingInfo.HiddenMethod.Parameters.IndexOf(hidingInfo.ParameterToReportOn) <
                hidingInfo.HiddenMethod.Parameters.Count() - 1;
        }

        private static bool IsMethodHidingOriginal(IMethodSymbol candidateHidingMethod, IMethodSymbol method)
        {
            for (int i = 0; i < candidateHidingMethod.Parameters.Length; i++)
            {
                if (!candidateHidingMethod.Parameters[i].Type.Equals(method.Parameters[i].Type))
                {
                    return false;
                }

                if (candidateHidingMethod.Parameters[i].IsOptional != method.Parameters[i].IsOptional)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
