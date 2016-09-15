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

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class UseValueParameter : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3237";
        internal const string Title = "\"value\" parameters should be used";
        internal const string Description =
            "In property and indexer \"set\" methods, and in event \"add\" and \"remove\" methods, the implicit \"value\" parameter " +
            "holds the value the accessor was called with. Not using the \"value\" means that the accessor ignores the caller's " +
            "intent which could cause unexpected results at runtime.";
        internal const string MessageFormat = "Use the \"value\" parameter in this {0} accessor declaration.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterCodeBlockStartActionInNonGenerated<SyntaxKind>(
                cbc =>
                {
                    if (cbc.SemanticModel.Compilation.IsTest())
                    {
                        return;
                    }

                    var accessorDeclaration = cbc.CodeBlock as AccessorDeclarationSyntax;
                    if (accessorDeclaration == null ||
                        accessorDeclaration.IsKind(SyntaxKind.GetAccessorDeclaration))
                    {
                        return;
                    }

                    if (accessorDeclaration.Body.Statements.Count == 1 &&
                        accessorDeclaration.Body.Statements.Single() is ThrowStatementSyntax)
                    {
                        return;
                    }

                    var foundValueReference = false;
                    cbc.RegisterSyntaxNodeAction(
                        c =>
                        {
                            var identifier = (IdentifierNameSyntax)c.Node;
                            var parameter = c.SemanticModel.GetSymbolInfo(identifier).Symbol as IParameterSymbol;

                            if (identifier.Identifier.ValueText == "value" &&
                                parameter != null &&
                                parameter.IsImplicitlyDeclared)
                            {
                                foundValueReference = true;
                            }
                        },
                        SyntaxKind.IdentifierName);

                    cbc.RegisterCodeBlockEndAction(
                        c =>
                        {
                            if (!foundValueReference)
                            {
                                var accessorType = GetAccessorType(accessorDeclaration);
                                c.ReportDiagnostic(Diagnostic.Create(Rule, accessorDeclaration.Keyword.GetLocation(), accessorType));
                            }
                        });
                });
        }

        private static string GetAccessorType(AccessorDeclarationSyntax accessorDeclaration)
        {
            string accessorType;

            if (accessorDeclaration.IsKind(SyntaxKind.AddAccessorDeclaration) ||
                accessorDeclaration.IsKind(SyntaxKind.RemoveAccessorDeclaration))
            {
                accessorType = "event";
            }
            else
            {
                var accessorList = accessorDeclaration.Parent;
                if (accessorList == null)
                {
                    return null;
                }
                var indexerOrProperty = accessorList.Parent;
                if (indexerOrProperty is IndexerDeclarationSyntax)
                {
                    accessorType = "indexer set";
                }
                else if (indexerOrProperty is PropertyDeclarationSyntax)
                {
                    accessorType = "property set";
                }
                else
                {
                    return null;
                }
            }

            return accessorType;
        }
    }
}
