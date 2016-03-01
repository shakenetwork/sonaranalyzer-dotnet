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

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cert, Tag.Misra, Tag.Pitfall)]
    public class ParameterNamesInPartialMethod : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S927";
        internal const string Title = "\"partial\" method parameter names should match";
        internal const string Description =
            "When the parameters to the implementation of a \"partial\" method don't match those " +
            "in the signature declaration, then confusion is almost guaranteed. Either the implementer was " +
            "confused when he renamed, swapped or mangled the parameter names in the implementation, or " +
            "callers will be confused.";
        internal const string MessageFormat = "Rename parameter \"{0}\" to \"{1}\".";
        internal const string Category = SonarLint.Common.Category.Design;
        internal const Severity RuleSeverity = Severity.Critical;
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
                    var methodSyntax = (MethodDeclarationSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(methodSyntax);

                    if (methodSymbol == null ||
                        methodSymbol.PartialDefinitionPart == null)
                    {
                        return;
                    }

                    var implementationParameters = methodSyntax.ParameterList.Parameters;
                    var definitionParameters = methodSymbol.PartialDefinitionPart.Parameters;

                    for (var i = 0; i < implementationParameters.Count && i < definitionParameters.Length; i++)
                    {
                        var implementationParameter = implementationParameters[i];

                        var definitionParameter = definitionParameters[i];
                        var implementationParameterName = implementationParameter.Identifier.ValueText;
                        if (implementationParameterName != definitionParameter.Name)
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule,
                                implementationParameter.Identifier.GetLocation(),
                                implementationParameterName, definitionParameter.Name));
                        }
                    }
                },
                SyntaxKind.MethodDeclaration);
        }
    }
}
