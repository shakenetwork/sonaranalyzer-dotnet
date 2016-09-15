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
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("2min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Confusing)]
    public class ExceptionRethrow : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3445";
        internal const string Title = "Exceptions should not be explicitly rethrown";
        internal const string Description =
            "When rethrowing an exception, you should do it by simply calling \"throw;\" and not \"throw exc;\", because the " +
            "stack trace is reset with the second syntax, making debugging a lot harder.";
        internal const string MessageFormat = "Consider using \"throw;\" to preserve the stack trace.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
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
                    var catchClause = (CatchClauseSyntax)c.Node;
                    if (catchClause.Declaration == null ||
                        catchClause.Declaration.Identifier.IsKind(SyntaxKind.None))
                    {
                        return;
                    }

                    var exceptionIdentifier = c.SemanticModel.GetDeclaredSymbol(catchClause.Declaration);
                    if (exceptionIdentifier == null)
                    {
                        return;
                    }

                    var throws = catchClause.DescendantNodes(n =>
                            n == catchClause ||
                            !n.IsKind(SyntaxKind.CatchClause))
                        .OfType<ThrowStatementSyntax>()
                        .Where(t => t.Expression != null);

                    foreach (var @throw in throws)
                    {
                        var thrown = c.SemanticModel.GetSymbolInfo(@throw.Expression).Symbol as ILocalSymbol;
                        if (object.Equals(thrown, exceptionIdentifier))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, @throw.GetLocation()));
                        }
                    }
                },
                SyntaxKind.CatchClause);
        }
    }
}
