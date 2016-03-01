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
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SynchronizationReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.MultiThreading)]
    public class LockOnThisOrType : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2551";
        internal const string Title = "Types and \"this\" should not be used for locking";
        internal const string Description =
            "Locking on the current object instance (i.e. \"this\"), or on a \"Type\" object increases the chance of " +
            "deadlocks because any other thread could acquire (or attempt to acquire) the same lock for another unrelated " +
            "purpose.";
        internal const string MessageFormat = "Lock on a new \"object\" instead.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = false;

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
                    var lockStatement = (LockStatementSyntax) c.Node;

                    if (LockOnThis(lockStatement.Expression) ||
                        LockOnSystemType(lockStatement.Expression, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, lockStatement.Expression.GetLocation()));
                    }
                },
                SyntaxKind.LockStatement);
        }

        private static bool LockOnSystemType(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            return semanticModel.GetTypeInfo(expression).Type.Is(KnownType.System_Type);
        }

        private static bool LockOnThis(ExpressionSyntax expression)
        {
            return expression.IsKind(SyntaxKind.ThisExpression);
        }
    }
}
