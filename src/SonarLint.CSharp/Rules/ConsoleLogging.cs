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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SecurityFeatures)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Security, Tag.OwaspA6)]
    public class ConsoleLogging : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2228";
        internal const string Title = "Console logging should not be used";
        internal const string Description =
            "Debug statements are always useful during development. But include them in production " +
            "code - particularly in code that runs client-side - and you run the risk of " +
            "inadvertently exposing sensitive information.";
        internal const string MessageFormat = "Remove this logging statement.";
        internal const string Category = SonarLint.Common.Category.Portability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        private static readonly string[] BannedConsoleMembers = { "WriteLine", "Write" };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var methodCall = (InvocationExpressionSyntax) c.Node;
                    var methodSymbol = c.SemanticModel.GetSymbolInfo(methodCall.Expression).Symbol;

                    if (methodSymbol != null &&
                        methodSymbol.IsInType(KnownType.System_Console) &&
                        BannedConsoleMembers.Contains(methodSymbol.Name))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, methodCall.Expression.GetLocation()));
                    }
                },
                SyntaxKind.InvocationExpression);
        }
    }
}
