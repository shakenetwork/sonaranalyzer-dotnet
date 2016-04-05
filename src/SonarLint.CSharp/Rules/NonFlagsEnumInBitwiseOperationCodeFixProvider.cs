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
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace SonarLint.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class NonFlagsEnumInBitwiseOperationCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add [Flags] to enum declaration";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(NonFlagsEnumInBitwiseOperation.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true);

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var operation = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
            var enumDeclaration = operation?.ReturnType?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken)
                as EnumDeclarationSyntax;

            if (enumDeclaration == null)
            {
                return;
            }

            var flagsAttributeType = semanticModel.Compilation.GetTypeByMetadataName("System.FlagsAttribute");
            if (flagsAttributeType == null)
            {
                return;
            }

            var currentSolution = context.Document.Project.Solution;
            var documentId = currentSolution.GetDocumentId(enumDeclaration.SyntaxTree);

            if (documentId == null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    async c =>
                    {
                        var enumDeclarationRoot = await currentSolution.GetDocument(documentId).GetSyntaxRootAsync(c);

                        var flagsAttributeName = flagsAttributeType.ToMinimalDisplayString(semanticModel, enumDeclaration.SpanStart);
                        flagsAttributeName = flagsAttributeName.Remove(flagsAttributeName.IndexOf("Attribute", System.StringComparison.Ordinal));

                        var attributes = enumDeclaration.AttributeLists.Add(
                            SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(new[] {
                                SyntaxFactory.Attribute(SyntaxFactory.ParseName(flagsAttributeName)) })));

                        var newDeclaration = enumDeclaration.WithAttributeLists(attributes);
                        var newRoot = enumDeclarationRoot.ReplaceNode(
                            enumDeclaration,
                            newDeclaration);
                        return currentSolution.WithDocumentSyntaxRoot(documentId, newRoot);
                    }),
                context.Diagnostics);
        }
    }
}
