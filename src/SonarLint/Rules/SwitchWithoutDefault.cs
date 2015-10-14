/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Rules.Common;

namespace SonarLint.Rules.CSharp
{
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureChangeability)]
    [Tags(Tag.Cert, Tag.Cwe, Tag.Misra)]
    public class SwitchWithoutDefault : SwitchWithoutDefaultBase<SyntaxKind>
    {
        private static readonly ImmutableArray<SyntaxKind> kindsOfInterest = ImmutableArray.Create(SyntaxKind.SwitchStatement);
        public override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => kindsOfInterest;

        protected override bool TryGetDiagnostic(SyntaxNode node, out Diagnostic diagnostic)
        {
            diagnostic = null;
            var switchNode = (SwitchStatementSyntax)node;
            if(!HasDefaultLabel(switchNode))
            {
                diagnostic = Diagnostic.Create(Rule, switchNode.SwitchKeyword.GetLocation(), "default", "switch");
                return true;
            }

            return false;
        }
        private static bool HasDefaultLabel(SwitchStatementSyntax node)
        {
            return node.Sections.Any(section => section.Labels.Any(labels => labels.IsKind(SyntaxKind.DefaultSwitchLabel)));
        }
    }
}

namespace SonarLint.Rules.VisualBasic
{
    using Microsoft.CodeAnalysis.VisualBasic;
    using Microsoft.CodeAnalysis.VisualBasic.Syntax;

    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureChangeability)]
    [Tags(Tag.Cert, Tag.Cwe, Tag.Misra)]
    public class SwitchWithoutDefault : SwitchWithoutDefaultBase<SyntaxKind>
    {
        private static readonly ImmutableArray<SyntaxKind> kindsOfInterest = ImmutableArray.Create(SyntaxKind.SelectBlock);
        public override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => kindsOfInterest;

        protected override bool TryGetDiagnostic(SyntaxNode node, out Diagnostic diagnostic)
        {
            diagnostic = null;
            var switchNode = (SelectBlockSyntax)node;
            if (!HasDefaultLabel(switchNode))
            {
                diagnostic = Diagnostic.Create(Rule, switchNode.SelectStatement.SelectKeyword.GetLocation(), "Case Else", "Select");
                return true;
            }

            return false;
        }
        private static bool HasDefaultLabel(SelectBlockSyntax node)
        {
            return node.CaseBlocks.Any(section => section.IsKind(SyntaxKind.CaseElseBlock));
        }
    }
}
