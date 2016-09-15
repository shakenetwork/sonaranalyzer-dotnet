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

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, true /* we are keeping this rule for SQ, CS0078 does the same in the IDE */)]
    [Tags(Tag.Cert, Tag.Convention, Tag.Misra, Tag.Pitfall)]
    public class LiteralSuffixUpperCase : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S818";
        internal const string Title = "Literal suffixes should be upper case";
        internal const string Description =
            "Using upper case literal suffixes removes the potential ambiguity between \"1\" (digit 1) and \"l\" " +
            "(letter el) for declaring literals.";
        internal const string MessageFormat = "Upper case this literal suffix.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), false,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var literal = (LiteralExpressionSyntax)c.Node;
                    var text = literal.Token.Text;

                    if (text[text.Length - 1] == 'l' &&
                        c.SemanticModel.GetTypeInfo(literal).Type.Is(KnownType.System_Int64))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(literal.SyntaxTree,
                            new TextSpan(literal.Span.End - 1, 1))));
                    }
                },
                SyntaxKind.NumericLiteralExpression);
        }
    }
}
