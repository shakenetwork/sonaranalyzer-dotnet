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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class ConstructorOverridableCall : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1699";
        internal const string Title = "Constructors should only call non-overridable methods";
        internal const string Description =
            "Calling an overridable method from a constructor could result in failures or strange behaviors when instantiating " +
            "a subclass which overrides the method.";
        internal const string MessageFormat = "Remove this call from a constructor to the overridable \"{0}\" method.";
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
                    var invocationExpression = (InvocationExpressionSyntax) c.Node;

                    var calledOn = (invocationExpression.Expression as MemberAccessExpressionSyntax)?.Expression;
                    var isCalledOnValid = calledOn == null || calledOn is ThisExpressionSyntax;
                    if (!isCalledOnValid)
                    {
                        return;
                    }

                    var methodSymbol =
                        c.SemanticModel.GetSymbolInfo(invocationExpression.Expression).Symbol as IMethodSymbol;
                    var enclosingSymbol = c.SemanticModel.GetEnclosingSymbol(invocationExpression.SpanStart) as IMethodSymbol;
                    if (methodSymbol == null ||
                        enclosingSymbol == null ||
                        enclosingSymbol.MethodKind != MethodKind.Constructor)
                    {
                        return;
                    }

                    if ((methodSymbol.IsVirtual || methodSymbol.IsAbstract) &&
                        enclosingSymbol.IsInType(methodSymbol.ContainingType))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, invocationExpression.Expression.GetLocation(),
                            methodSymbol.Name));
                    }
                },
                SyntaxKind.InvocationExpression);
        }
    }
}
