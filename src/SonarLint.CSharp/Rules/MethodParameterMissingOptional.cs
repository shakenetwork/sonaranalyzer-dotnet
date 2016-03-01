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
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class MethodParameterMissingOptional : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3450";
        internal const string Title = "Parameters with \"[DefaultParameterValue]\" attributes should also be marked with \"[Optional]\"";
        internal const string Description =
            "There is no point in providing a default value for a parameter if callers are required to provide a value for it anyway. Thus, " +
            "\"[DefaultParameterValue]\" should always be used in conjunction with \"[Optional]\".";
        internal const string MessageFormat = "Add the \"[Optional]\" attribute to this parameter.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
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
                    if (!parameter.AttributeLists.Any())
                    {
                        return;
                    }

                    var attributes = GetAttributesForParameter(parameter, c.SemanticModel)
                        .ToList();

                    var defaultParameterValueAttribute = attributes
                        .FirstOrDefault(a => a.Symbol.IsInType(KnownType.System_Runtime_InteropServices_DefaultParameterValueAttribute));

                    if (defaultParameterValueAttribute == null)
                    {
                        return;
                    }

                    var optionalAttribute = attributes
                        .FirstOrDefault(a => a.Symbol.IsInType(KnownType.System_Runtime_InteropServices_OptionalAttribute));

                    if (optionalAttribute == null)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, defaultParameterValueAttribute.SyntaxNode.GetLocation()));
                    }
                },
                SyntaxKind.Parameter);
        }

        internal static IEnumerable<AttributeSyntaxSymbolMapping> GetAttributesForParameter(ParameterSyntax parameter, SemanticModel semanticModel)
        {
            return parameter.AttributeLists
                .SelectMany(al => al.Attributes)
                .Select(attr => new AttributeSyntaxSymbolMapping
                {
                    SyntaxNode = attr,
                    Symbol = semanticModel.GetSymbolInfo(attr).Symbol as IMethodSymbol
                })
                .Where(attr => attr.Symbol != null);
        }

        internal class AttributeSyntaxSymbolMapping
        {
            public AttributeSyntax SyntaxNode { get; set; }
            public IMethodSymbol Symbol { get; set; }
        }
    }
}
