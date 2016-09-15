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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cert, Tag.Misra, Tag.Pitfall)]
    public class MethodOverrideChangedDefaultValue : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1006";
        internal const string Title = "Method overrides should not change parameter defaults";
        internal const string Description =
            "Default arguments are determined by the static type of the object. If a default argument is different for a parameter in " +
            "an overriding method, the value used in the call will be different when calls are made via the base or derived object, " +
            "which may be contrary to developer expectations. Default parameter values are useless in explicit interface implementations, " +
            "because the static type of the object will always be the implemented interface. Thus, specifying the default values is " +
            "useless and confusing.";
        internal const string MessageFormat = "{0} the default parameter value {1}.";
        internal const string MessageAdd = "defined in the overridden method";
        internal const string MessageRemove = "to match the signature of overridden method";
        internal const string MessageUseSame = "defined in the overridden method";
        internal const string MessageRemoveExplicit = "from this explicit interface implementation";
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
                    var method = (MethodDeclarationSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(method);

                    IMethodSymbol overriddenMember;
                    if (methodSymbol == null ||
                        !methodSymbol.TryGetOverriddenOrInterfaceMember(out overriddenMember))
                    {
                        return;
                    }

                    for (int i = 0; i < methodSymbol.Parameters.Length; i++)
                    {
                        var overridingParameter = methodSymbol.Parameters[i];
                        var overriddenParameter = overriddenMember.Parameters[i];

                        var parameterSyntax = method.ParameterList.Parameters[i];

                        ReportParameterIfNeeded(overridingParameter, overriddenParameter, parameterSyntax,
                            isExplicitImplementation: methodSymbol.ExplicitInterfaceImplementations.Any(),
                            context: c);
                    }
                },
                SyntaxKind.MethodDeclaration);
        }

        private static void ReportParameterIfNeeded(IParameterSymbol overridingParameter, IParameterSymbol overriddenParameter,
            ParameterSyntax parameterSyntax, bool isExplicitImplementation, SyntaxNodeAnalysisContext context)
        {
            if (isExplicitImplementation)
            {
                if (overridingParameter.HasExplicitDefaultValue)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, parameterSyntax.Default.GetLocation(), "Remove", MessageRemoveExplicit));
                }

                return;
            }

            if (overridingParameter.HasExplicitDefaultValue &&
                !overriddenParameter.HasExplicitDefaultValue)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, parameterSyntax.Default.GetLocation(), "Remove", MessageRemove));
                return;
            }

            if (!overridingParameter.HasExplicitDefaultValue &&
                overriddenParameter.HasExplicitDefaultValue)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, parameterSyntax.Identifier.GetLocation(), "Add", MessageAdd));
                return;
            }

            if (overridingParameter.HasExplicitDefaultValue &&
                overriddenParameter.HasExplicitDefaultValue &&
                !object.Equals(overridingParameter.ExplicitDefaultValue, overriddenParameter.ExplicitDefaultValue))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, parameterSyntax.Default.Value.GetLocation(), "Use", MessageUseSame));
                return;
            }
        }
    }
}
