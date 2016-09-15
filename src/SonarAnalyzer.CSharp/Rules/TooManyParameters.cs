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
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.UnitTestability)]
    [SqaleConstantRemediation("20min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.BrainOverload)]
    public class TooManyParameters : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S107";
        internal const string Title = "Methods should not have too many parameters";
        internal const string Description =
            "A long parameter list can indicate that a new structure should be created to wrap the numerous parameters or that the function is doing " +
            "too many things.";
        internal const string MessageFormat = "{2} has {1} parameters, which is greater than the {0} authorized.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const int DefaultValueMaximum = 7;
        [RuleParameter("max", PropertyType.Integer, "Maximum authorized number of parameters", DefaultValueMaximum)]
        public int Maximum { get; set; } = DefaultValueMaximum;

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var parameterListNode = (ParameterListSyntax)c.Node;
                    var parameters = parameterListNode.Parameters.Count;

                    string declarationName;

                    if (parameters > Maximum &&
                        parameterListNode.Parent != null &&
                        CanBeChanged(parameterListNode.Parent, c.SemanticModel) &&
                        Mapping.TryGetValue(parameterListNode.Parent.Kind(), out declarationName))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, parameterListNode.GetLocation(),
                            Maximum, parameters, declarationName));
                    }
                },
                SyntaxKind.ParameterList);
        }

        private static bool CanBeChanged(SyntaxNode node, SemanticModel semanticModel)
        {
            var declaredSymbol = semanticModel.GetDeclaredSymbol(node);
            var symbol = semanticModel.GetSymbolInfo(node).Symbol;

            if (declaredSymbol == null && symbol == null)
            {
                // No information
                return false;
            }

            if (symbol != null)
            {
                // Not a declaration, such as Action
                return true;
            }

            return !declaredSymbol.IsInterfaceImplementationOrMemberOverride();
        }

        private static readonly Dictionary<SyntaxKind, string> Mapping = new Dictionary<SyntaxKind, string>
        {
            { SyntaxKind.ConstructorDeclaration, "Constructor" },
            { SyntaxKind.MethodDeclaration, "Method" },
            { SyntaxKind.DelegateDeclaration, "Delegate" },
            { SyntaxKind.AnonymousMethodExpression, "Delegate" },
            { SyntaxKind.ParenthesizedLambdaExpression, "Lambda" },
            { SyntaxKind.SimpleLambdaExpression, "Lambda" }
        };
    }
}
