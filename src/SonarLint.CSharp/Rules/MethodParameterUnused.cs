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
using System.Linq;
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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Misra, Tag.Unused)]
    public class MethodParameterUnused : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1172";
        internal const string Title = "Unused method parameters should be removed";
        internal const string Description =
            "Unused parameters are misleading. Whatever the value passed to such parameters is, the behavior will be the same.";
        internal const string MessageFormat = "Remove this unused method parameter \"{0}\".";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockStartActionInNonGenerated<SyntaxKind>(
                cbc =>
                {
                    var methodDeclaration = cbc.CodeBlock as MethodDeclarationSyntax;
                    var methodSymbol = cbc.OwningSymbol as IMethodSymbol;
                    if (methodDeclaration == null ||
                        methodSymbol == null ||
                        !IsMethodCandidate(methodSymbol, cbc.SemanticModel.Compilation))
                    {
                        return;
                    }

                    var usedParameters = ImmutableHashSet<IParameterSymbol>.Empty;

                    cbc.RegisterSyntaxNodeAction(
                        c =>
                        {
                            var identifier = (IdentifierNameSyntax)c.Node;
                            var parameter = c.SemanticModel.GetSymbolInfo(identifier).Symbol as IParameterSymbol;
                            if (parameter != null &&
                                methodSymbol.Parameters.Contains(parameter))
                            {
                                usedParameters = usedParameters.Add(parameter);
                            }
                        },
                        SyntaxKind.IdentifierName);

                    cbc.RegisterCodeBlockEndAction(
                        c =>
                        {
                            var unusedParameters = methodSymbol.Parameters.Except(usedParameters);

                            if (methodSymbol.IsExtensionMethod)
                            {
                                unusedParameters = unusedParameters.Except(new[] { methodSymbol.Parameters.First() });
                            }

                            foreach (var unusedParameter in unusedParameters)
                            {
                                var reference = unusedParameter.DeclaringSyntaxReferences.FirstOrDefault();
                                if (reference == null)
                                {
                                    continue;
                                }

                                var parameter = reference.GetSyntax() as ParameterSyntax;
                                if (parameter == null)
                                {
                                    continue;
                                }

                                var location = parameter.Identifier.GetLocation();
                                c.ReportDiagnostic(Diagnostic.Create(Rule, location, unusedParameter.Name));
                            }
                        });
                });
        }

        private static bool IsMethodCandidate(IMethodSymbol methodSymbol, Compilation compilation)
        {
            return !methodSymbol.IsAbstract &&
                !methodSymbol.IsVirtual &&
                !IsMethodProbablyEventHandler(methodSymbol, compilation) &&
                !methodSymbol.IsInterfaceImplementationOrMemberOverride();
        }

        private static bool IsMethodProbablyEventHandler(IMethodSymbol methodSymbol, Compilation compilation)
        {
            if (!methodSymbol.ReturnsVoid ||
                methodSymbol.Parameters.Length != 2)
            {
                return false;
            }

            var eventArgs = methodSymbol.Parameters[1];
            var eventArgsType = eventArgs.Type as INamedTypeSymbol;
            if (eventArgsType == null)
            {
                return true;
            }

            var sysEventArgs = compilation.GetTypeByMetadataName("System.EventArgs");
            if (sysEventArgs == null)
            {
                return true;
            }

            return eventArgsType.DerivesOrImplementsAny(sysEventArgs);
        }
    }
}
