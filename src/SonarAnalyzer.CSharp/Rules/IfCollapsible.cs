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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class IfCollapsible : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1066";
        internal const string Title = "Collapsible \"if\" statements should be merged";
        internal const string Description = "Merging collapsible \"if\" statements increases the code's readability.";
        internal const string MessageFormat = "Merge this if statement with the enclosing one.";
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
                    var ifStatement = (IfStatementSyntax) c.Node;

                    if (ifStatement.Else != null)
                    {
                        return;
                    }

                    var parentIfStatment = GetParentIfStatement(ifStatement);

                    if (parentIfStatment != null &&
                        parentIfStatment.Else == null)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, ifStatement.IfKeyword.GetLocation()));
                    }
                },
                SyntaxKind.IfStatement);
        }

        private static IfStatementSyntax GetParentIfStatement(IfStatementSyntax ifStatement)
        {
            var parent = ifStatement.Parent;

            while (parent is BlockSyntax)
            {
                var block = (BlockSyntax) parent;

                if (block.Statements.Count != 1)
                {
                    return null;
                }

                parent = parent.Parent;
            }

            var parentIfStatement = parent as IfStatementSyntax;
            return parentIfStatement;
        }
    }
}
