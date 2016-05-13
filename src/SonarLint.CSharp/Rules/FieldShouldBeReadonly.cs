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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    using FieldTuple = SyntaxNodeSymbolSemanticModelTuple<VariableDeclaratorSyntax, IFieldSymbol>;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Confusing)]
    public class FieldShouldBeReadonly : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2933";
        internal const string Title = "Fields that are only assigned in the constructor should be \"readonly\"";
        internal const string Description =
            "\"readonly\" fields can only be assigned in a class constructor. If a class has " +
            "a field that's not marked \"readonly\" but is only set in the constructor, it " +
            "could cause confusion about the field's intended use. To avoid confusion, such " +
            "fields should be marked \"readonly\" to make their intended use explicit, and to " +
            "prevent future maintainers from inadvertently changing their use.";
        internal const string MessageFormat = "Make \"{0}\" \"readonly\".";
        internal const string Category = SonarLint.Common.Category.Design;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly ISet<SyntaxKind> assignmentKinds = ImmutableHashSet.Create(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.RightShiftAssignmentExpression);

        private static readonly ISet<SyntaxKind> prefixUnaryKinds = ImmutableHashSet.Create(
            SyntaxKind.PreDecrementExpression,
            SyntaxKind.PreIncrementExpression);

        private static readonly ISet<SyntaxKind> postfixUnaryKinds = ImmutableHashSet.Create(
            SyntaxKind.PostDecrementExpression,
            SyntaxKind.PostIncrementExpression);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var declaredSymbol = (INamedTypeSymbol)c.Symbol;
                    if (!declaredSymbol.IsClassOrStruct())
                    {
                        return;
                    }

                    if (declaredSymbol.DeclaringSyntaxReferences.Count() > 1)
                    {
                        // Partial classes are not processed.
                        // See https://github.com/dotnet/roslyn/issues/3748
                        return;
                    }

                    var assignedAsReadonly = new HashSet<IFieldSymbol>();
                    var nonCandidateFields = new HashSet<IFieldSymbol>();

                    var partialDeclarations = declaredSymbol.DeclaringSyntaxReferences
                        .Select(reference => reference.GetSyntax())
                        .OfType<TypeDeclarationSyntax>()
                        .Select(node =>
                            new SyntaxNodeSemanticModelTuple<TypeDeclarationSyntax>
                            {
                                SyntaxNode = node,
                                SemanticModel = c.Compilation.GetSemanticModel(node.SyntaxTree)
                            })
                        .Where(n => n.SemanticModel != null)
                        .ToList();

                    var fieldDeclarations = partialDeclarations
                        .Select(td =>
                            new
                            {
                                SemanticModel = td.SemanticModel,
                                Fields = td.SyntaxNode.DescendantNodes().OfType<FieldDeclarationSyntax>()
                            })
                        .SelectMany(t => t.Fields.SelectMany(f => GetCandidateFields(f, t.SemanticModel, assignedAsReadonly)))
                        .ToDictionary(f => f.Symbol, f => f.SyntaxNode);

                    var candidateFields = new HashSet<IFieldSymbol>(fieldDeclarations.Keys);

                    foreach (var partialDeclaration in partialDeclarations)
                    {
                        CollectFieldsFromDeclaration(partialDeclaration, assignedAsReadonly, nonCandidateFields);
                    }

                    var fields = assignedAsReadonly.Intersect(candidateFields).Except(nonCandidateFields);

                    foreach (var field in fields)
                    {
                        var identifier = fieldDeclarations[field].Identifier;

                        c.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule, identifier.GetLocation(), identifier.ValueText));
                    }
                },
                SymbolKind.NamedType);
        }

        private static void CollectFieldsFromDeclaration(SyntaxNodeSemanticModelTuple<TypeDeclarationSyntax> partialDeclaration,
            HashSet<IFieldSymbol> assignedAsReadonly, HashSet<IFieldSymbol> nonCandidateFields)
        {
            CollectFieldsFromAssignments(partialDeclaration, assignedAsReadonly, nonCandidateFields);
            CollectFieldsFromPrefixUnaryExpressions(partialDeclaration, assignedAsReadonly, nonCandidateFields);
            CollectFieldsFromPostfixUnaryExpressions(partialDeclaration, assignedAsReadonly, nonCandidateFields);
            CollectFieldsFromArguments(partialDeclaration, assignedAsReadonly, nonCandidateFields);
        }

        private static void CollectFieldsFromArguments(SyntaxNodeSemanticModelTuple<TypeDeclarationSyntax> partialDeclaration,
            HashSet<IFieldSymbol> assignedAsReadonly, HashSet<IFieldSymbol> nonCandidateFields)
        {
            var arguments = partialDeclaration.SyntaxNode.DescendantNodes()
                .OfType<ArgumentSyntax>();

            foreach (var argument in arguments)
            {
                ProcessArgument(argument, partialDeclaration.SemanticModel, assignedAsReadonly, nonCandidateFields);
            }
        }

        private static void CollectFieldsFromPostfixUnaryExpressions(SyntaxNodeSemanticModelTuple<TypeDeclarationSyntax> partialDeclaration,
            HashSet<IFieldSymbol> assignedAsReadonly, HashSet<IFieldSymbol> nonCandidateFields)
        {
            var postfixUnaries = partialDeclaration.SyntaxNode.DescendantNodes()
                .OfType<PostfixUnaryExpressionSyntax>()
                .Where(a => postfixUnaryKinds.Contains(a.Kind()));

            foreach (var postfixUnary in postfixUnaries)
            {
                ProcessExpression(postfixUnary.Operand, partialDeclaration.SemanticModel, assignedAsReadonly, nonCandidateFields);
            }
        }

        private static void CollectFieldsFromPrefixUnaryExpressions(SyntaxNodeSemanticModelTuple<TypeDeclarationSyntax> partialDeclaration,
            HashSet<IFieldSymbol> assignedAsReadonly, HashSet<IFieldSymbol> nonCandidateFields)
        {
            var prefixUnaries = partialDeclaration.SyntaxNode.DescendantNodes()
                .OfType<PrefixUnaryExpressionSyntax>()
                .Where(a => prefixUnaryKinds.Contains(a.Kind()));

            foreach (var prefixUnary in prefixUnaries)
            {
                ProcessExpression(prefixUnary.Operand, partialDeclaration.SemanticModel, assignedAsReadonly, nonCandidateFields);
            }
        }

        private static void CollectFieldsFromAssignments(SyntaxNodeSemanticModelTuple<TypeDeclarationSyntax> partialDeclaration,
            HashSet<IFieldSymbol> assignedAsReadonly, HashSet<IFieldSymbol> nonCandidateFields)
        {
            var assignments = partialDeclaration.SyntaxNode.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(a => assignmentKinds.Contains(a.Kind()));

            foreach (var assignment in assignments)
            {
                ProcessExpression(assignment.Left, partialDeclaration.SemanticModel, assignedAsReadonly, nonCandidateFields);
            }
        }

        private static List<FieldTuple> GetCandidateFields(FieldDeclarationSyntax fieldDeclaration, SemanticModel semanticModel,
            HashSet<IFieldSymbol> assignedAsReadonly)
        {
            var candidateFields = fieldDeclaration.Declaration.Variables
                .Select(variableDeclaratorSyntax => new FieldTuple
                {
                    SyntaxNode = variableDeclaratorSyntax,
                    Symbol = semanticModel.GetDeclaredSymbol(variableDeclaratorSyntax) as IFieldSymbol,
                    SemanticModel = semanticModel
                })
                .Where(f => f.Symbol != null)
                .Where(f => FieldIsRelevant(f.Symbol))
                .ToList();

            foreach (var field in candidateFields.Where(field => field.SyntaxNode.Initializer != null))
            {
                assignedAsReadonly.Add(field.Symbol);
            }

            return candidateFields;
        }

        private static void ProcessArgument(ArgumentSyntax argument, SemanticModel semanticModel,
            HashSet<IFieldSymbol> assignedAsReadonly, HashSet<IFieldSymbol> nonCandidateFields)
        {
            if (argument.RefOrOutKeyword.IsKind(SyntaxKind.None))
            {
                return;
            }

            // ref/out should be handled the same way as all other field assignments:
            ProcessExpression(argument.Expression, semanticModel, assignedAsReadonly, nonCandidateFields);
        }

        private static void ProcessExpression(ExpressionSyntax expression, SemanticModel semanticModel,
            HashSet<IFieldSymbol> assignedAsReadonly, HashSet<IFieldSymbol> nonCandidateFields)
        {
            var fieldSymbol = semanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
            if (fieldSymbol == null ||
                !FieldIsRelevant(fieldSymbol))
            {
                return;
            }

            if (!IsFieldOnThis(expression))
            {
                nonCandidateFields.Add(fieldSymbol);
                return;
            }

            var constructorSymbol = semanticModel.GetEnclosingSymbol(expression.SpanStart) as IMethodSymbol;
            if (constructorSymbol == null)
            {
                nonCandidateFields.Add(fieldSymbol);
                return;
            }

            if (constructorSymbol.MethodKind == MethodKind.Constructor &&
                constructorSymbol.ContainingType.Equals(fieldSymbol.ContainingType))
            {
                assignedAsReadonly.Add(fieldSymbol);
            }
            else
            {
                nonCandidateFields.Add(fieldSymbol);
            }
        }

        private static bool IsFieldOnThis(ExpressionSyntax expression)
        {
            if (expression.IsKind(SyntaxKind.IdentifierName))
            {
                return true;
            }

            var memberAccess = expression as MemberAccessExpressionSyntax;
            if (memberAccess != null &&
                memberAccess.Expression.IsKind(SyntaxKind.ThisExpression))
            {
                return true;
            }

            var conditionalAccess = expression as ConditionalAccessExpressionSyntax;
            if (conditionalAccess != null &&
                conditionalAccess.Expression.IsKind(SyntaxKind.ThisExpression))
            {
                return true;
            }

            return false;
        }

        private static bool FieldIsRelevant(IFieldSymbol fieldSymbol)
        {
            return fieldSymbol != null &&
                   !fieldSymbol.IsStatic &&
                   !fieldSymbol.IsConst &&
                   !fieldSymbol.IsReadOnly &&
                   fieldSymbol.DeclaredAccessibility == Accessibility.Private;
        }
    }
}
