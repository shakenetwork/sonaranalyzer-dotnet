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
using System.Linq;
using Microsoft.CodeAnalysis;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.Common
{
    public abstract class OptionalParameterBase : SonarDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S2360";
        protected const string Title = "Optional parameters should not be used";
        protected const string Description =
            "The overloading mechanism should be used in place of optional parameters for several reasons. " +
            "Optional parameter values are baked into the method call site code, thus, if a default " +
            "value has been changed, all referencing assemblies need to be rebuilt, otherwise the original values will be used. The " +
            "Common Language Specification (CLS) allows compilers to ignore default parameter values, and thus require the caller to " +
            "explicitly specify the values. The concept of optional argument exists only in VB.Net and C#. In all other languages " +
            "like C++ or Java, the overloading mechanism is the only way to get the same behavior. " +
            "Optional parameters prevent muddying the definition of the function contract. Here is a simple " +
            "example: if there are two optional parameters, when one is defined, is the second one still optional or mandatory?";
        protected const string MessageFormat = "Use the overloading mechanism instead of the optional parameters.";
        protected const string Category = SonarAnalyzer.Common.Category.Design;
        protected const Severity RuleSeverity = Severity.Major;
        protected const bool IsActivatedByDefault = true;

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }
    }

    public abstract class OptionalParameterBase<TLanguageKindEnum, TMethodSyntax, TParameterSyntax> : OptionalParameterBase
        where TLanguageKindEnum : struct
        where TMethodSyntax : SyntaxNode
        where TParameterSyntax: SyntaxNode
    {
        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var method = (TMethodSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(method);

                    if (symbol == null ||
                        !symbol.IsPublicApi() ||
                        symbol.IsInterfaceImplementationOrMemberOverride())
                    {
                        return;
                    }

                    var parameters = GetParameters(method);

                    foreach (var parameter in parameters.Where(p => IsOptional(p) && !HasAllowedAttribute(p, c.SemanticModel)))
                    {
                        var location = GetReportLocation(parameter);
                        c.ReportDiagnostic(Diagnostic.Create(Rule, location));
                    }
                },
                SyntaxKindsOfInterest.ToArray());
        }

        private static bool HasAllowedAttribute(TParameterSyntax parameterSyntax, SemanticModel semanticModel)
        {
            var parameterSymbol = semanticModel.GetDeclaredSymbol(parameterSyntax) as IParameterSymbol;

            return parameterSymbol == null ||
                parameterSymbol.GetAttributes().Any(attr => attr.AttributeClass.IsAny(KnownType.CallerInfoAttributes));
        }

        protected abstract IEnumerable<TParameterSyntax> GetParameters(TMethodSyntax method);
        protected abstract bool IsOptional(TParameterSyntax parameter);
        protected abstract Location GetReportLocation(TParameterSyntax parameter);
        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
    }
}
