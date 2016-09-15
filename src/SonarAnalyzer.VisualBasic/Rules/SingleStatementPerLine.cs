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
using System;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("1min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Tags(Tag.Convention)]
    public class SingleStatementPerLine : SingleStatementPerLineBase<StatementSyntax>
    {
        protected override bool StatementShouldBeExcluded(StatementSyntax statement)
        {
            return //statement == null ||
                StatementIsBlock(statement) ||
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

            var multiline = st.Parent as MultiLineLambdaExpressionSyntax;
            if (multiline == null)
            {
                return false;
            }

            return multiline.Statements.Count == 1;
        }

        private static bool StatementIsBlock(StatementSyntax st) =>
            ExcludedTypes.Any(bType => bType.IsInstanceOfType(st));

        private static readonly Type[] ExcludedTypes =
        {
            typeof(NamespaceBlockSyntax),
            typeof(TypeBlockSyntax),
            typeof(EnumBlockSyntax),
            typeof(MethodBlockBaseSyntax),
            typeof(PropertyBlockSyntax),
            typeof(EventBlockSyntax),
            typeof(DoLoopBlockSyntax),
            typeof(WhileBlockSyntax),
            typeof(ForOrForEachBlockSyntax),
            typeof(MultiLineIfBlockSyntax),
            typeof(ElseStatementSyntax),
            typeof(SyncLockBlockSyntax),
            typeof(TryBlockSyntax),
            typeof(UsingBlockSyntax),
            typeof(WithBlockSyntax),
            typeof(MultiLineLambdaExpressionSyntax),
            typeof(MethodBaseSyntax),
            typeof(InheritsOrImplementsStatementSyntax)
        };

        protected sealed override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.VisualBasic.GeneratedCodeRecognizer.Instance;
    }
}
