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

using System.Collections.Generic;
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
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Cwe, Tag.DenialOfService, Tag.Security)]
    public class DisposableMemberInNonDisposableClass : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2931";
        internal const string Title = "Classes with \"IDisposable\" members should implement \"IDisposable\"";
        internal const string Description =
            "Classes with \"IDisposable\" members are responsible for cleaning up those members " +
            "by calling their \"Dispose\" methods.The best practice here is for the owning class " +
            "to itself implement \"IDisposable\" and call its members' \"Dispose\" methods from " +
            "its own \"Dispose\" method.";
        internal const string MessageFormat = "Implement \"IDisposable\" in this class and use the \"Dispose\" method to call \"Dispose\" on {0}.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly Accessibility[] Accessibilities = { Accessibility.Protected, Accessibility.Private };

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterCompilationStartAction(analysisContext =>
            {
                if (analysisContext.Compilation.IsTest())
                {
                    return;
                }

                var fieldsByNamedType = MultiValueDictionary<INamedTypeSymbol, IFieldSymbol>.Create<HashSet<IFieldSymbol>>();
                var fieldsAssigned = ImmutableHashSet<IFieldSymbol>.Empty;

                analysisContext.RegisterSymbolAction(c =>
                {
                    var namedTypeSymbol = (INamedTypeSymbol)c.Symbol;
                    if (!namedTypeSymbol.IsClass() ||
                        namedTypeSymbol.Implements(KnownType.System_IDisposable))
                    {
                        return;
                    }

                    var disposableFields = namedTypeSymbol.GetMembers()
                        .OfType<IFieldSymbol>()
                        .Where(IsNonStaticNonPublicDisposableField)
                        .ToImmutableHashSet();

                    fieldsByNamedType.AddRangeWithKey(namedTypeSymbol, disposableFields);
                }, SymbolKind.NamedType);


                analysisContext.RegisterSyntaxNodeAction(c =>
                {
                    var assignment = (AssignmentExpressionSyntax)c.Node;
                    var expression = assignment.Right;
                    var fieldSymbol = c.SemanticModel.GetSymbolInfo(assignment.Left).Symbol as IFieldSymbol;

                    fieldsAssigned = AddFieldIfNeeded(fieldSymbol, expression, fieldsAssigned);
                }, SyntaxKind.SimpleAssignmentExpression);

                analysisContext.RegisterSyntaxNodeAction(c =>
                {
                    var field = (FieldDeclarationSyntax)c.Node;

                    foreach (var variableDeclaratorSyntax in field.Declaration.Variables
                        .Where(declaratorSyntax => declaratorSyntax.Initializer != null))
                    {
                        var fieldSymbol = c.SemanticModel.GetDeclaredSymbol(variableDeclaratorSyntax) as IFieldSymbol;

                        fieldsAssigned = AddFieldIfNeeded(fieldSymbol, variableDeclaratorSyntax.Initializer.Value,
                            fieldsAssigned);
                    }

                }, SyntaxKind.FieldDeclaration);

                analysisContext.RegisterCompilationEndAction(c =>
                {
                    foreach (var kv in fieldsByNamedType)
                    {
                        foreach (var classSyntax in kv.Key.DeclaringSyntaxReferences
                            .Select(declaringSyntaxReference => declaringSyntaxReference.GetSyntax())
                            .OfType<ClassDeclarationSyntax>())
                        {
                            var assignedFields = kv.Value.Intersect(fieldsAssigned).ToList();

                            if (!assignedFields.Any())
                            {
                                continue;
                            }
                            var variableNames = string.Join(", ",
                                assignedFields.Select(symbol => $"\"{symbol.Name}\"").OrderBy(s => s));

                            c.ReportDiagnosticIfNonGenerated(
                                Diagnostic.Create(Rule, classSyntax.Identifier.GetLocation(), variableNames),
                                c.Compilation);
                        }
                    }
                });
            });
        }

        private static ImmutableHashSet<IFieldSymbol> AddFieldIfNeeded(IFieldSymbol fieldSymbol, ExpressionSyntax expression,
            ImmutableHashSet<IFieldSymbol> fieldsAssigned)
        {
            var objectCreation = expression as ObjectCreationExpressionSyntax;
            if (objectCreation == null ||
                !IsNonStaticNonPublicDisposableField(fieldSymbol))
            {
                return fieldsAssigned;
            }

            return fieldsAssigned.Add(fieldSymbol);
        }

        internal static bool IsNonStaticNonPublicDisposableField(IFieldSymbol fieldSymbol)
        {
            return fieldSymbol != null &&
                   !fieldSymbol.IsStatic &&
                   Accessibilities.Contains(fieldSymbol.DeclaredAccessibility) &&
                   fieldSymbol.Type.Implements(KnownType.System_IDisposable);
        }
    }
}
