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
using Microsoft.CodeAnalysis.Text;
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.MaintainabilityCompliance)]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    public class IssueSuppression : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1309";
        internal const string Title = "Track uses of in-source issue suppressions";
        internal const string Description =
            "This rule allows you to track the usage of the \"SuppressMessage\" attributes and \"#pragma warning disable\" mechanism.";
        internal const string MessageFormat = "Do not suppress issues.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Info;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), true,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var attribute = (AttributeSyntax)c.Node;
                    var attributeConstructor = c.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol;

                    if (attributeConstructor == null ||
                        !attributeConstructor.ContainingType.Is(KnownType.System_Diagnostics_CodeAnalysis_SuppressMessageAttribute))
                    {
                        return;
                    }

                    var identifier = attribute.Name as IdentifierNameSyntax;
                    if (identifier == null)
                    {
                        identifier = (attribute.Name as QualifiedNameSyntax)?.Right as IdentifierNameSyntax;
                    }

                    if (identifier != null)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation()));
                    }
                },
                SyntaxKind.Attribute);

            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    foreach (var token in c.Tree.GetRoot().DescendantTokens())
                    {
                        CheckTrivias(token.LeadingTrivia, c);
                        CheckTrivias(token.TrailingTrivia, c);
                    }
                });
        }

        private static void CheckTrivias(SyntaxTriviaList triviaList, SyntaxTreeAnalysisContext c)
        {
            var pragmaWarnings = triviaList
                .Where(t => t.HasStructure)
                .Select(t => t.GetStructure())
                .OfType<PragmaWarningDirectiveTriviaSyntax>()
                .Where(t => t.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword));

            foreach (var pragmaWarning in pragmaWarnings)
            {
                var location = Location.Create(pragmaWarning.SyntaxTree,
                    TextSpan.FromBounds(pragmaWarning.SpanStart, pragmaWarning.DisableOrRestoreKeyword.Span.End));
                c.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        }
    }
}
