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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class DisposableReturnedFromUsing : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2997";
        internal const string Title = "\"IDisposables\" created in a \"using\" statement should not be returned";
        internal const string Description =
            "Typically you want to use \"using\" to create a local \"IDisposable\" variable; it will trigger disposal " +
            "of the object when control passes out of the block's scope. The exception to this rule is when your method " +
            "returns that \"IDisposable\". In that case \"using\" disposes of the object before the caller can make use " +
            "of it, likely causing exceptions at runtime. So you should either remove \"using\" or avoid returning the " +
            "\"IDisposable\".";
        internal const string MessageFormat = "Remove the \"using\" statement; it will cause automatic disposal of {0}.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Blocker;
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
                    var usingStatement = (UsingStatementSyntax) c.Node;
                    var declaration = usingStatement.Declaration;
                    var declaredSymbols = ImmutableHashSet<ISymbol>.Empty;
                    if (declaration != null)
                    {
                        declaredSymbols =
                            declaration.Variables.Select(syntax => c.SemanticModel.GetDeclaredSymbol(syntax))
                                .Where(symbol => symbol != null)
                                .ToImmutableHashSet();
                    }
                    else
                    {
                        var assignment = usingStatement.Expression as AssignmentExpressionSyntax;
                        if (assignment != null)
                        {
                            var identifierName = assignment.Left as IdentifierNameSyntax;
                            if (identifierName == null)
                            {
                                return;
                            }
                            var symbol = c.SemanticModel.GetSymbolInfo(identifierName).Symbol;
                            if (symbol == null)
                            {
                                return;
                            }
                            declaredSymbols = (new[] { symbol }).ToImmutableHashSet();
                        }
                    }

                    if (declaredSymbols.IsEmpty)
                    {
                        return;
                    }

                    var returnedSymbols = GetReturnedSymbols(usingStatement.Statement, c.SemanticModel);
                    returnedSymbols = returnedSymbols.Intersect(declaredSymbols);

                    if (returnedSymbols.Any())
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, usingStatement.UsingKeyword.GetLocation(),
                            string.Join(", ",
                                returnedSymbols.Select(s => $"\"{s.Name}\"").OrderBy(s => s))));
                    }
                },
                SyntaxKind.UsingStatement);
        }

        private static ImmutableHashSet<ISymbol> GetReturnedSymbols(StatementSyntax usingStatement,
            SemanticModel semanticModel)
        {
            var enclosingSymbol = semanticModel.GetEnclosingSymbol(usingStatement.SpanStart);

            return usingStatement.DescendantNodesAndSelf()
                .OfType<ReturnStatementSyntax>()
                .Where(ret => semanticModel.GetEnclosingSymbol(ret.SpanStart).Equals(enclosingSymbol))
                .Select(ret => ret.Expression)
                .OfType<IdentifierNameSyntax>()
                .Select(identifier => semanticModel.GetSymbolInfo(identifier).Symbol)
                .Where(symbol => symbol != null)
                .ToImmutableHashSet();
        }
    }
}
