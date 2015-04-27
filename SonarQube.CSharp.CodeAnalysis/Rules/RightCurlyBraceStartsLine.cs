/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [SqaleConstantRemediation("1min")]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("convention")]
    public class RightCurlyBraceStartsLine : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1109";
        internal const string Description = "A close curly brace should be located at the beginning of a line";
        internal const string MessageFormat = "Move this closing curly brace to the next line.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Minor; 
        internal const bool IsActivatedByDefault = false;

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS1109");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeAction(
                c =>
                {
                    var root = c.Tree.GetRoot();
                    foreach (var closeBraceToken in GetDescendantCloseBraceTokens(root)
                        .Where(closeBraceToken =>
                            !StartsLine(closeBraceToken) &&
                            !IsOnSameLineAsOpenBrace(closeBraceToken) &&
                            !IsInitializer(closeBraceToken.Parent)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, closeBraceToken.GetLocation()));
                    }
                });
        }

        private static bool StartsLine(SyntaxToken token)
        {
            return token.GetPreviousToken().GetLocation().GetLineSpan().EndLinePosition.Line != token.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        private static bool IsOnSameLineAsOpenBrace(SyntaxToken closeBraceToken)
        {
            var openBraceToken = closeBraceToken.Parent.ChildTokens().Single(token => token.IsKind(SyntaxKind.OpenBraceToken));
            return openBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line == closeBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        private static bool IsInitializer(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.ArrayInitializerExpression) ||
                node.IsKind(SyntaxKind.CollectionInitializerExpression) ||
                node.IsKind(SyntaxKind.AnonymousObjectCreationExpression) ||
                node.IsKind(SyntaxKind.ObjectInitializerExpression);
        }

        private static IEnumerable<SyntaxToken> GetDescendantCloseBraceTokens(SyntaxNode node)
        {
            return node.DescendantTokens().Where(token => token.IsKind(SyntaxKind.CloseBraceToken));
        }
    }
}
