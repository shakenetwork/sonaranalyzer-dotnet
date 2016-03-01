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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.BadPractice, Tag.BrainOverload)]
    public class ExitStatementUsage : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3385";
        internal const string Title = "\"Exit\" statements should not be used";
        internal const string Description =
            "Other than \"Exit Select\", using an \"Exit\" statement is never a good idea. \"Exit Do\", \"Exit For\", \"Exit Try\", and " +
            "\"Exit While\" will all result in unstructured control flow, i.e.spaghetti code. \"Exit Function\", \"Exit Property\", and " +
            "\"Exit Sub\" are all poor, less-readable substitutes for a simple return, and if used with code that should return a value " +
            "(\"Exit Function\" and in some cases \"Exit Property\") they could result in a \"NullReferenceException\". This rule raises " +
            "an issue for all uses of \"Exit\" except \"Exit Select\" and \"Exit Do\" statements in loops without condition.";
        internal const string MessageFormat = "Remove this \"Exit\" statement.";
        internal const string Category = SonarLint.Common.Category.Reliability;
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
                c => c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation())),
                SyntaxKind.ExitForStatement,
                SyntaxKind.ExitFunctionStatement,
                SyntaxKind.ExitPropertyStatement,
                SyntaxKind.ExitSubStatement,
                SyntaxKind.ExitTryStatement,
                SyntaxKind.ExitWhileStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var parent = c.Node.Parent;
                    while(parent != null &&
                        !(parent is DoLoopBlockSyntax))
                    {
                        parent = parent.Parent;
                    }

                    if (parent == null ||
                        parent.IsKind(SyntaxKind.SimpleDoLoopBlock))
                    {
                        return;
                    }

                    c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation()));
                },
                SyntaxKind.ExitDoStatement);
        }
    }
}
