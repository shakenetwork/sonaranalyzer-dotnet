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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.CSharp;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("1h")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.UnitTestability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.BrainOverload)]
    public class FunctionComplexity : ParameteredDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1541";
        internal const string Title = "Methods should not be too complex";
        internal const string Description =
           "The cyclomatic complexity of a function should not exceed a defined threshold. Complex code can perform poorly and will in any case " +
            "be difficult to understand and therefore to maintain.";
        internal const string MessageFormat = "The Cyclomatic Complexity of this method is {1} which is greater than {0} authorized.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const int DefaultValueMaximum = 10;

        [RuleParameter("maximumFunctionComplexityThreshold", PropertyType.Integer,
            "The maximum authorized complexity in function", DefaultValueMaximum)]
        public int Maximum { get; set; } = DefaultValueMaximum;

        public override void Initialize(WrappingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<MethodDeclarationSyntax>(c, m => m.Identifier.GetLocation()),
                SyntaxKind.MethodDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<OperatorDeclarationSyntax>(c, o => o.OperatorKeyword.GetLocation()),
                SyntaxKind.OperatorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<ConstructorDeclarationSyntax>(c, co => co.Identifier.GetLocation()),
                SyntaxKind.ConstructorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<DestructorDeclarationSyntax>(c, d => d.Identifier.GetLocation()),
                SyntaxKind.DestructorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<AccessorDeclarationSyntax>(c, a => a.Keyword.GetLocation()),
                SyntaxKind.GetAccessorDeclaration,
                SyntaxKind.SetAccessorDeclaration,
                SyntaxKind.AddAccessorDeclaration,
                SyntaxKind.RemoveAccessorDeclaration);
        }

        private void CheckComplexity<TSyntax>(SyntaxNodeAnalysisContext c, Func<TSyntax, Location> location)
            where TSyntax : SyntaxNode
        {
            var complexity = new Metrics(c.Node.SyntaxTree).GetComplexity(c.Node);
            if (complexity > Maximum)
            {
                var syntax = (TSyntax)c.Node;
                c.ReportDiagnostic(Diagnostic.Create(Rule, location(syntax), Maximum, complexity));
            }
        }
    }
}
