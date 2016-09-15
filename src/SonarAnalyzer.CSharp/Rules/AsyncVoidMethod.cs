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

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SynchronizationReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class AsyncVoidMethod : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3168";
        internal const string Title = "\"async\" methods should not return \"void\"";
        internal const string Description =
            "An \"async\" method with a \"void\" return type is a \"fire and forget\" method best reserved for event " +
            "handlers because there's no way to wait for the method's execution to complete and respond accordingly. " +
            "There's also no way to \"catch\" exceptions thrown from the method. Having an \"async void\" method that " +
            "is not an event handler could mean your program works some times and not others because of timing issues. " +
            "Instead, \"async\" methods should return \"Task\".";
        internal const string MessageFormat = "Return \"Task\" instead.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
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
                    var methodDeclaration = (MethodDeclarationSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(methodDeclaration);

                    if (methodSymbol != null &&
                        IsMethodCandidate(methodSymbol))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.ReturnType.GetLocation()));
                    }
                },
                SyntaxKind.MethodDeclaration);
        }

        private static bool IsMethodCandidate(IMethodSymbol methodSymbol)
        {
            return methodSymbol.IsAsync &&
                methodSymbol.ReturnsVoid &&
                methodSymbol.IsChangeable() &&
                !methodSymbol.IsProbablyEventHandler();
        }
    }
}
