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
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class GenericReadonlyFieldPropertyAssignment : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2934";
        internal const string Title =
            "Property assignments should not be made for \"readonly\" fields not constrained to reference types";
        internal const string Description =
            "While the properties of a \"readonly\" reference type field can still be changed after " +
            "initialization, those of a \"readonly\" value field, such as a \"struct\", cannot. If the " +
            "member could be either a \"class\" or a \"struct\" then assignment to its properties could " +
            "be unreliable, working sometimes but not others.";
        internal const string MessageFormat =
            "Restrict \"{0}\" to be a reference type or remove this assignment of \"{1}\"; it is useless if \"{0}\" " +
            "is a value type.";
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
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var assignment = (AssignmentExpressionSyntax)c.Node;
                    var expression = assignment.Left;

                    ProcessPropertyChange(expression, c.SemanticModel, c);
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

            context.RegisterSyntaxNodeActionInNonGenerated(
                    c =>
                    {
                        var unary = (PrefixUnaryExpressionSyntax)c.Node;
                        var expression = unary.Operand;

                        ProcessPropertyChange(expression, c.SemanticModel, c);
                    },
                    SyntaxKind.PreDecrementExpression,
                    SyntaxKind.PreIncrementExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var unary = (PostfixUnaryExpressionSyntax)c.Node;
                    var expression = unary.Operand;

                    ProcessPropertyChange(expression, c.SemanticModel, c);
                },
                SyntaxKind.PostDecrementExpression,
                SyntaxKind.PostIncrementExpression);
        }

        private static void ProcessPropertyChange(ExpressionSyntax expression, SemanticModel semanticModel,
            SyntaxNodeAnalysisContext context)
        {
            var memberAccess = expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
            {
                return;
            }

            var propertySymbol = semanticModel.GetSymbolInfo(expression).Symbol as IPropertySymbol;
            if (propertySymbol == null)
            {
                return;
            }

            var fieldSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol as IFieldSymbol;
            if (!IsFieldReadonlyAndPossiblyValueType(fieldSymbol) ||
                IsInsideConstructorDeclaration(expression, fieldSymbol.ContainingType, semanticModel))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation(), fieldSymbol.Name, propertySymbol.Name));
        }

        private static bool IsFieldReadonlyAndPossiblyValueType(IFieldSymbol fieldSymbol)
        {
            return fieldSymbol != null &&
                fieldSymbol.IsReadOnly &&
                GenericParameterMightBeValueType(fieldSymbol.Type as ITypeParameterSymbol);
        }

        private static bool IsInsideConstructorDeclaration(ExpressionSyntax expression, INamedTypeSymbol currentType,
            SemanticModel semanticModel)
        {
            var constructorSymbol = semanticModel.GetEnclosingSymbol(expression.SpanStart) as IMethodSymbol;
            return constructorSymbol != null &&
                constructorSymbol.MethodKind == MethodKind.Constructor &&
                constructorSymbol.ContainingType.Equals(currentType);
        }

        private static bool GenericParameterMightBeValueType(ITypeParameterSymbol typeParameterSymbol)
        {
            if (typeParameterSymbol == null ||
                typeParameterSymbol.HasReferenceTypeConstraint ||
                typeParameterSymbol.HasValueTypeConstraint ||
                typeParameterSymbol.ConstraintTypes.OfType<IErrorTypeSymbol>().Any())
            {
                return false;
            }

            return typeParameterSymbol.ConstraintTypes
                .Select(constraintType => MightBeValueType(constraintType))
                .All(basedOnPossiblyValueType => basedOnPossiblyValueType);
        }

        private static bool MightBeValueType(ITypeSymbol type)
        {
            return type.IsInterface() ||
                GenericParameterMightBeValueType(type as ITypeParameterSymbol);
        }
    }
}
