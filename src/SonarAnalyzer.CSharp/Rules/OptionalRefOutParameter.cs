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

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class OptionalRefOutParameter : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3447";
        internal const string Title = "\"[Optional]\" should not be used on \"ref\" or \"out\" parameters";
        internal const string Description =
            "The use of \"ref\" or \"out\" in combination with \"[Optional]\" is both confusing and contradictory. \"[Optional]\" indicates " +
            "that the parameter doesn't have to be provided, while out and ref mean that the parameter will be used to return data to the " +
            "caller. Thus, making it \"[Optional]\" to provide the parameter in which you will be passing back the method results doesn't " +
            "make sense.";
        internal const string MessageFormat = "Remove the \"Optional\" attribute, it cannot be used with \"{0}\".";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
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
                    var parameter = (ParameterSyntax)c.Node;
                    if (!parameter.AttributeLists.Any() ||
                        !parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword) || m.IsKind(SyntaxKind.OutKeyword)))
                    {
                        return;
                    }

                    var optionalAttribute = AttributeSyntaxSymbolMapping.GetAttributesForParameter(parameter, c.SemanticModel)
                        .FirstOrDefault(a =>
                            a.Symbol.IsInType(KnownType.System_Runtime_InteropServices_OptionalAttribute));

                    if (optionalAttribute != null)
                    {
                        var refKind = parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.OutKeyword)) ? "out" : "ref";
                        c.ReportDiagnostic(Diagnostic.Create(Rule, optionalAttribute.SyntaxNode.GetLocation(), refKind));
                    }
                },
                SyntaxKind.Parameter);
        }
    }
}
