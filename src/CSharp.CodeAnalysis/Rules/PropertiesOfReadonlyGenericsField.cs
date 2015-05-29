/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
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
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug")]
    public class PropertiesOfReadonlyGenericsField : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2934";
        internal const string Description = "Property assignments should not be made for \"readonly\" fields not constrained to reference types";
        internal const string MessageFormat = "Restrict \"{0}\" to be a reference type or remove this assignment of \"{1}\"; it is useless if \"{0}\" is a value type.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2934");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
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

            context.RegisterSyntaxNodeAction(
                    c =>
                    {
                        var unary = (PrefixUnaryExpressionSyntax)c.Node;
                        var expression = unary.Operand;

                        ProcessPropertyChange(expression, c.SemanticModel, c);
                    },
                    SyntaxKind.PreDecrementExpression,
                    SyntaxKind.PreIncrementExpression);

            context.RegisterSyntaxNodeAction(
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
            if (fieldSymbol == null ||
                !fieldSymbol.IsReadOnly ||
                !RelevantFieldType(fieldSymbol.Type))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation(), fieldSymbol.Name, propertySymbol.Name));
        }

        private static bool RelevantFieldType(ITypeSymbol type)
        {
            var typeParameterSymbol = type as ITypeParameterSymbol;
            return typeParameterSymbol != null &&
                   !typeParameterSymbol.HasReferenceTypeConstraint &&
                   !typeParameterSymbol.ConstraintTypes.OfType<IErrorTypeSymbol>().Any() &&
                   !typeParameterSymbol.ConstraintTypes.Any(typeSymbol =>
                       typeSymbol.IsReferenceType &&
                       typeSymbol.TypeKind == TypeKind.Class);
        }
    }
}
