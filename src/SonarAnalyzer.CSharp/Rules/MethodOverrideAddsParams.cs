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
    [SqaleConstantRemediation("1min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Confusing)]
    public class MethodOverrideAddsParams : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3600";
        internal const string Title = "\"params\" should not be introduced on overrides";
        internal const string Description =
            "Adding \"params\" to a method override has no effect. The compiler accepts it, but the callers won't be " +
            "able to benefit from the added modifier.";
        internal const string MessageFormat = "\"params\" should be removed from this override.";
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
                    var method = (MethodDeclarationSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(method);

                    if (methodSymbol == null ||
                        !methodSymbol.IsOverride ||
                        methodSymbol.OverriddenMethod == null)
                    {
                        return;
                    }

                    var lastParameter = method.ParameterList.Parameters.LastOrDefault();
                    if (lastParameter == null)
                    {
                        return;
                    }

                    var paramsKeyword = lastParameter.Modifiers.FirstOrDefault(
                        modifier => modifier.IsKind(SyntaxKind.ParamsKeyword));

                    if (paramsKeyword != default(SyntaxToken) &&
                        IsNotSemanticallyParams(lastParameter, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, paramsKeyword.GetLocation()));
                    }
                },
                SyntaxKind.MethodDeclaration);
        }

        private static bool IsNotSemanticallyParams(ParameterSyntax parameter, SemanticModel semanticModel)
        {
            var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter);
            return parameterSymbol != null &&
                !parameterSymbol.IsParams;
        }
    }
}
