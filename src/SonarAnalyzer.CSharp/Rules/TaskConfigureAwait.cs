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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SynchronizationReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.MultiThreading, Tag.Suspicious)]
    public class TaskConfigureAwait : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3216";
        internal const string Title = "\"ConfigureAwait(false)\" should be used";
        internal const string Description =
            "After an \"await\"ed \"Task\" has executed, you can continue execution in the original, calling thread or " +
            "any arbitrary thread. Unless the rest of the code needs the context from which the \"Task\" was spawned, " +
            "\"Task.ConfigureAwait(false)\" should be used to keep execution in the \"Task\" thread to avoid the need " +
            "of context switching and the possibility of deadlocks. This rule raises an issue when code in a class " +
            "library \"await\"s a \"Task\" and continues execution in the main thread.";
        internal const string MessageFormat =
            "Add \".ConfigureAwait(false)\" to this call to allow execution to continue in any thread.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
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
                    if (c.SemanticModel.Compilation.IsTest() ||
                        c.SemanticModel.Compilation.Options.OutputKind != OutputKind.DynamicallyLinkedLibrary)
                    {
                        //this rule only makes sense in libraries
                        return;
                    }

                    var awaitExpression = (AwaitExpressionSyntax)c.Node;
                    var expression = awaitExpression.Expression;
                    if (expression == null)
                    {
                        return;
                    }

                    var type = c.SemanticModel.GetTypeInfo(expression).Type;
                    if (type.DerivesFrom(KnownType.System_Threading_Tasks_Task))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation()));
                    }
                },
                SyntaxKind.AwaitExpression);
        }
    }
}
