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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using SonarLint.Rules.Common;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Formatting;

namespace SonarLint.Rules
{
    namespace CSharp
    {
        using Microsoft.CodeAnalysis.CSharp;
        using Microsoft.CodeAnalysis.CSharp.Syntax;

        [ExportCodeFixProvider(LanguageNames.CSharp)]
        public class MultipleVariableDeclarationCodeFixProvider : MultipleVariableDeclarationCodeFixProviderBase
        {
            protected override SyntaxNode CalculateNewRoot(SyntaxNode root, SyntaxNode node)
            {
                var declarator = node as VariableDeclaratorSyntax;
                if (declarator == null)
                {
                    return root;
                }

                var declaration = declarator.Parent as VariableDeclarationSyntax;
                if (declaration == null)
                {
                    return root;
                }

                var newDeclarations = declaration.Variables.Select(variable =>
                    SyntaxFactory.VariableDeclaration(
                        declaration.Type.WithoutTrailingTrivia(),
                        SyntaxFactory.SeparatedList(new[] { variable.WithLeadingTrivia(GetLeadingTriviaFor(variable)) })));

                IEnumerable<SyntaxNode> newNodes;

                var fieldDeclaration = declaration.Parent as FieldDeclarationSyntax;
                if (fieldDeclaration == null)
                {
                    var localDeclaration = declaration.Parent as LocalDeclarationStatementSyntax;
                    if (localDeclaration == null)
                    {
                        return root;
                    }

                    newNodes = newDeclarations
                        .Select(decl =>
                            SyntaxFactory.LocalDeclarationStatement(
                                localDeclaration.Modifiers,
                                decl));
                }
                else
                {
                    newNodes = newDeclarations
                        .Select(decl =>
                            SyntaxFactory.FieldDeclaration(
                                fieldDeclaration.AttributeLists,
                                fieldDeclaration.Modifiers,
                                decl));
                }

                return root.ReplaceNode(declaration.Parent, newNodes);
            }

            private IEnumerable<SyntaxTrivia> GetLeadingTriviaFor(VariableDeclaratorSyntax variable)
            {
                var previousToken = variable.GetFirstToken().GetPreviousToken();
                return previousToken.TrailingTrivia
                    .Concat(variable.GetLeadingTrivia());
            }
        }
    }

    namespace VisualBasic
    {
        using Microsoft.CodeAnalysis.VisualBasic;
        using Microsoft.CodeAnalysis.VisualBasic.Syntax;
        using System;

        [ExportCodeFixProvider(LanguageNames.VisualBasic)]
        public class MultipleVariableDeclarationCodeFixProvider : MultipleVariableDeclarationCodeFixProviderBase
        {
            protected override SyntaxNode CalculateNewRoot(SyntaxNode root, SyntaxNode node)
            {
                var identifier = node as ModifiedIdentifierSyntax;
                if (identifier == null)
                {
                    return root;
                }

                var declarator = identifier.Parent as VariableDeclaratorSyntax;
                if (declarator == null)
                {
                    return root;
                }

                IEnumerable<SyntaxNode> newNodes;

                var fieldDeclaration = declarator.Parent as FieldDeclarationSyntax;
                if (fieldDeclaration == null)
                {
                    var localDeclaration = declarator.Parent as LocalDeclarationStatementSyntax;
                    if (localDeclaration == null)
                    {
                        return root;
                    }

                    newNodes = localDeclaration.Declarators.SelectMany(decl =>
                        GetConvertedDeclarators(decl).Select(newDecl =>
                            SyntaxFactory.LocalDeclarationStatement(
                                localDeclaration.Modifiers,
                                SyntaxFactory.SeparatedList(new[] { newDecl }))));
                }
                else
                {
                    newNodes = fieldDeclaration.Declarators.SelectMany(decl =>
                        GetConvertedDeclarators(decl).Select(newDecl =>
                            SyntaxFactory.FieldDeclaration(
                                fieldDeclaration.AttributeLists,
                                fieldDeclaration.Modifiers,
                                SyntaxFactory.SeparatedList(new[] { newDecl }))));
                }

                return root.ReplaceNode(declarator.Parent, newNodes);
            }

            private static IEnumerable<VariableDeclaratorSyntax> GetConvertedDeclarators(VariableDeclaratorSyntax declarator)
            {
                var declarators = declarator.Names.Select(n =>
                    SyntaxFactory.VariableDeclarator(
                        SyntaxFactory.SeparatedList(new[] { n }),
                        declarator.AsClause,
                        null))
                        .ToList();

                if (declarator.Initializer != null)
                {
                    var last = declarators.Last();
                    last = last.WithInitializer(declarator.Initializer);
                    declarators[declarators.Count - 1] = last;
                }

                return declarators.Select(d =>
                    d.WithTrailingTrivia(SyntaxFactory.EndOfLineTrivia(Environment.NewLine))
                     .WithAdditionalAnnotations(Formatter.Annotation));
            }
        }
    }
}