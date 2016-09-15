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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Rules.Common;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("1min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Tags(Tag.Convention)]
    public class SingleStatementPerLine : SingleStatementPerLineBase<StatementSyntax>
    {
        protected override bool StatementShouldBeExcluded(StatementSyntax statement)
        {
            return StatementIsBlock(statement) ||
                StatementIsSingleInLambda(statement);
        }

        private static bool StatementIsSingleInLambda(StatementSyntax st)
        {
            if (st.DescendantNodes()
                .OfType<StatementSyntax>()
                .Any())
            {
                return false;
            }

            var parentBlock = st.Parent as BlockSyntax;
            if (parentBlock == null ||
                parentBlock.Statements.Count > 1)
            {
                return false;
            }

            return parentBlock.Parent is AnonymousFunctionExpressionSyntax;
        }

        private static bool StatementIsBlock(StatementSyntax st) => st is BlockSyntax;

        protected sealed override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.CSharp.GeneratedCodeRecognizer.Instance;
    }
}
