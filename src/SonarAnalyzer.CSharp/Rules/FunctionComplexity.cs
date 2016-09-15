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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.CSharp;
using SonarAnalyzer.Helpers;
using System.Collections.Immutable;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    public class FunctionComplexity : FunctionComplexityBase
    {
        protected const string Title = "Methods and properties should not be too complex";
        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<MethodDeclarationSyntax>(c, m => m.Identifier.GetLocation(), "method"),
                SyntaxKind.MethodDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<PropertyDeclarationSyntax>(c, p => p.ExpressionBody, p => p.Identifier.GetLocation(), "property"),
                SyntaxKind.PropertyDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<OperatorDeclarationSyntax>(c, o => o.OperatorKeyword.GetLocation(), "operator"),
                SyntaxKind.OperatorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<ConstructorDeclarationSyntax>(c, co => co.Identifier.GetLocation(), "constructor"),
                SyntaxKind.ConstructorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<DestructorDeclarationSyntax>(c, d => d.Identifier.GetLocation(), "destructor"),
                SyntaxKind.DestructorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<AccessorDeclarationSyntax>(c, a => a.Keyword.GetLocation(), "accessor"),
                SyntaxKind.GetAccessorDeclaration,
                SyntaxKind.SetAccessorDeclaration,
                SyntaxKind.AddAccessorDeclaration,
                SyntaxKind.RemoveAccessorDeclaration);
        }

        protected override int GetComplexity(SyntaxNode node) =>
            new Metrics(node.SyntaxTree).GetComplexity(node);

        protected sealed override GeneratedCodeRecognizer GeneratedCodeRecognizer =>
            Helpers.CSharp.GeneratedCodeRecognizer.Instance;
    }
}
