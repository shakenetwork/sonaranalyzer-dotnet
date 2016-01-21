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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SonarLint.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class LiteralSuffixUpperCaseCodeFixProvider : CodeFixProvider
    {
        public const string Title = "Make literal suffix upper case";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(LiteralSuffixUpperCase.DiagnosticId);
            }
        }
        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var literal = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true) as LiteralExpressionSyntax;
            if (literal == null)
            {
                return;
            }
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            var newLiteral = GetNewLiteral(literal, semanticModel);

            if (!newLiteral.IsKind(SyntaxKind.None))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        c =>
                        {
                            var newRoot = root.ReplaceNode(literal,
                                literal.WithToken(newLiteral).WithTriviaFrom(literal));
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        }),
                    context.Diagnostics);
            }
        }

        private static SyntaxToken GetNewLiteral(LiteralExpressionSyntax literal, SemanticModel semanticModel)
        {
            var type = semanticModel.GetTypeInfo(literal).Type;
            var text = literal.Token.Text;
            var reversedText = text.Reverse().ToList();
            var reversedTextEnding = new string(reversedText.TakeWhile(char.IsLetter).ToArray());
            var reversedTextBeginning = new string(reversedText.SkipWhile(char.IsLetter).ToArray());
            var newText = new string((reversedTextEnding.ToUpperInvariant() + reversedTextBeginning).Reverse().ToArray());

            switch (type.SpecialType)
            {
                case SpecialType.System_Int32:
                    return SyntaxFactory.Literal(
                        newText,
                        (int)literal.Token.Value);
                case SpecialType.System_Char:
                    return SyntaxFactory.Literal(
                        newText,
                        (char)literal.Token.Value);
                case SpecialType.System_UInt32:
                    return SyntaxFactory.Literal(
                        newText,
                        (uint)literal.Token.Value);
                case SpecialType.System_Int64:
                    return SyntaxFactory.Literal(
                        newText,
                        (long)literal.Token.Value);
                case SpecialType.System_UInt64:
                    return SyntaxFactory.Literal(
                        newText,
                        (ulong)literal.Token.Value);
                case SpecialType.System_Decimal:
                    return SyntaxFactory.Literal(
                        newText,
                        (decimal)literal.Token.Value);
                case SpecialType.System_Single:
                    return SyntaxFactory.Literal(
                        newText,
                        (float)literal.Token.Value);
                case SpecialType.System_Double:
                    return SyntaxFactory.Literal(
                        newText,
                        (double)literal.Token.Value);
                default:
                    return SyntaxFactory.Token(SyntaxKind.None);
            }
        }
    }
}
