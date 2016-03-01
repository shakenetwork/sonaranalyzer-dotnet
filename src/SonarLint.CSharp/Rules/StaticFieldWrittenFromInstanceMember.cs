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
using Microsoft.CodeAnalysis.Text;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SynchronizationReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.MultiThreading)]
    public class StaticFieldWrittenFromInstanceMember : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2696";
        internal const string Title = "Instance members should not write to \"static\" fields";
        internal const string Description =
            "Correctly updating a \"static\" field from a non-static method is tricky to get right and could easily lead to " +
            "bugs if there are multiple class instances and/or multiple threads in play.";
        internal const string MessageFormat = "Make the enclosing {0} \"static\" or remove this set.";
        internal const string Category = SonarLint.Common.Category.Reliability;
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
                    SyntaxNode declaration = cbc.CodeBlock as MethodDeclarationSyntax;
                    var declarationType = "method";

                    if (declaration == null)
                    {
                        declaration = cbc.CodeBlock as AccessorDeclarationSyntax;
                        declarationType = "property";
                        if (declaration == null)
                        {
                            return;
                        }
                    }

                    var methodOrPropertySymbol = cbc.OwningSymbol;
                    if (methodOrPropertySymbol == null ||
                        methodOrPropertySymbol.IsStatic)
                    {
                        return;
                    }

                    cbc.RegisterSyntaxNodeAction(
                    c =>
                    {
                        var assignment = (AssignmentExpressionSyntax)c.Node;
                        var expression = assignment.Left;

                        if (IsStaticFieldModification(expression, c.SemanticModel))
                        {
                            var location = Location.Create(expression.SyntaxTree,
                                new TextSpan(expression.SpanStart,
                                    assignment.OperatorToken.Span.End - expression.SpanStart));

                            c.ReportDiagnostic(Diagnostic.Create(Rule, location, declarationType));
                        }
                    },
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

                    cbc.RegisterSyntaxNodeAction(
                        c =>
                        {
                            var unary = (PrefixUnaryExpressionSyntax)c.Node;
                            var expression = unary.Operand;

                            if (IsStaticFieldModification(expression, c.SemanticModel))
                            {
                                c.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation(), declarationType));
                            }
                        },
                        SyntaxKind.PreDecrementExpression,
                        SyntaxKind.PreIncrementExpression);

                    cbc.RegisterSyntaxNodeAction(
                        c =>
                        {
                            var unary = (PostfixUnaryExpressionSyntax)c.Node;
                            var expression = unary.Operand;

                            if (IsStaticFieldModification(expression, c.SemanticModel))
                            {
                                c.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation(), declarationType));
                            }
                        },
                        SyntaxKind.PostDecrementExpression,
                        SyntaxKind.PostIncrementExpression);
                });
        }

        private static bool IsStaticFieldModification(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var fieldSymbol = semanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
            return fieldSymbol != null &&
                   fieldSymbol.IsStatic;
        }
    }
}
