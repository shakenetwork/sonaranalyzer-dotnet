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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious)]
    public class PartialMethodNoImplementation : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3251";
        internal const string Title = "Implementations should be provided for \"partial\" methods";
        internal const string Description =
            "\"partial\" methods allow an increased degree of flexibility in programming a system. Hooks can be added to generated code " +
            "by invoking methods that define their signature, but might not have an implementation yet. But if the implementation is " +
            "still missing when the code makes it to production, the compiler silently removes the call. In the best case scenario, " +
            "such calls simply represent cruft, but in they worst case they are critical, missing functionality, the loss of which will " +
            "lead to unexpected results at runtime.";
        internal const string MessageFormat = "Supply an implementation for {0} partial method{1}.";
        internal const string MessageAdditional = ", otherwise this call will be ignored";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckForCandidatePartialInvocation(c),
                SyntaxKind.InvocationExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckForCandidatePartialDeclaration(c),
                SyntaxKind.MethodDeclaration);
        }

        private static void CheckForCandidatePartialDeclaration(SyntaxNodeAnalysisContext context)
        {
            var declaration = (MethodDeclarationSyntax)context.Node;
            var partialKeyword = declaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.PartialKeyword));
            if (declaration.Body != null ||
                partialKeyword == default(SyntaxToken))
            {
                return;
            }

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(declaration);
            if (methodSymbol != null &&
                methodSymbol.PartialImplementationPart == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, partialKeyword.GetLocation(), "this", string.Empty));
            }
        }

        private static void CheckForCandidatePartialInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol == null)
            {
                return;
            }

            // from the method symbol it's not possible to tell if it's a partial method or not.
            // https://github.com/dotnet/roslyn/issues/48

            var partialDeclarations = methodSymbol.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax())
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Body == null && method.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

            if (methodSymbol.PartialImplementationPart == null &&
                partialDeclarations.Any())
            {
                var statement = invocation.Parent as StatementSyntax;
                if (statement == null)
                {
                    return;
                }
                context.ReportDiagnostic(Diagnostic.Create(Rule, statement.GetLocation(), "the", MessageAdditional));
            }
        }
    }
}

