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

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Misra, Tag.Pitfall)]
    public class MethodOverrideChangedDefaultValue : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1006";
        internal const string Title = "Method overrides should use the same default parameter values as the base methods";
        internal const string Description =
            "Default arguments are determined by the static type of the object. If a default argument is different for a parameter in " +
            "an overriding method, the value used in the call will be different when calls are made via the base or derived object, " +
            "which may be contrary to developer expectations.";
        internal const string MessageFormat = "{0}";
        internal const string MessageAdd = "Add the default parameter value defined in the overridden method.";
        internal const string MessageRemove = "Remove the default parameter value to match the signature of overridden method.";
        internal const string MessageUseSame = "Use the default parameter value defined in the overridden method. ";
        internal const string Category = SonarLint.Common.Category.Reliability;
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
                        var derivedParameter = methodSymbol.Parameters[i];
                        var overriddenParameter = overriddenMember.Parameters[i];

                        var parameterSyntax = method.ParameterList.Parameters[i];

                        if (derivedParameter.HasExplicitDefaultValue && !overriddenParameter.HasExplicitDefaultValue)
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, parameterSyntax.Default.GetLocation(), MessageRemove));
                            continue;
                        }

                        if (!derivedParameter.HasExplicitDefaultValue && overriddenParameter.HasExplicitDefaultValue)
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, parameterSyntax.Identifier.GetLocation(), MessageAdd));
                            continue;
                        }

                        if (derivedParameter.HasExplicitDefaultValue &&
                            overriddenParameter.HasExplicitDefaultValue &&
                            !object.Equals(derivedParameter.ExplicitDefaultValue, overriddenParameter.ExplicitDefaultValue))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, parameterSyntax.Default.Value.GetLocation(), MessageUseSame));
                            continue;
                        }
                    }
                },
                SyntaxKind.MethodDeclaration);
        }
    }
}
