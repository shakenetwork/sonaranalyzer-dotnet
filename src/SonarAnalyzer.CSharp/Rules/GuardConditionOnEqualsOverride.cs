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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class GuardConditionOnEqualsOverride : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3397";
        internal const string Title = "\"base.Equals\" should not be used to check for reference equality in \"Equals\" if \"base\" is not \"object\"";
        internal const string Description =
            "\"object.Equals()\" overrides can be optimized by checking first for reference equality between \"this\" and the " +
            "parameter. This check can be implemented by calling \"object.ReferenceEquals()\" or \"base.Equals()\", where \"base\" " +
            "is \"object\". However, using \"base.Equals()\" is a maintenance hazard because while it works if you extend \"Object\" " +
            "directly, if you introduce a new base class that overrides \"Equals\", it suddenly stops working.";
        internal const string MessageFormat = "Change this guard condition to call \"object.ReferenceEquals\".";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly ISet<string> MethodNames = ImmutableHashSet.Create( GetHashCodeEqualsOverride.EqualsName );

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterCodeBlockStartActionInNonGenerated<SyntaxKind>(
                cb =>
                {
                    var methodDeclaration = cb.CodeBlock as MethodDeclarationSyntax;
                    if (methodDeclaration == null)
                    {
                        return;
                    }

                    var methodSymbol = cb.OwningSymbol as IMethodSymbol;
                    if (methodSymbol == null ||
                        !GetHashCodeEqualsOverride.MethodIsRelevant(methodSymbol, MethodNames))
                    {
                        return;
                    }

                    cb.RegisterSyntaxNodeAction(
                        c =>
                        {
                            CheckInvocationInsideMethod(c, methodSymbol);
                        },
                        SyntaxKind.InvocationExpression);
                });
        }
        private static void CheckInvocationInsideMethod(SyntaxNodeAnalysisContext context,
            IMethodSymbol methodSymbol)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var invokedMethod = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (invokedMethod == null ||
                invokedMethod.Name != methodSymbol.Name)
            {
                return;
            }

            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;

            var baseCall = memberAccess?.Expression as BaseExpressionSyntax;
            if (baseCall == null)
            {
                return;
            }

            var objectType = invokedMethod.ContainingType;
            if (objectType != null &&
                !objectType.Is(KnownType.System_Object) &&
                GetHashCodeEqualsOverride.IsEqualsCallInGuardCondition(invocation, invokedMethod))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            }
        }
    }
}
