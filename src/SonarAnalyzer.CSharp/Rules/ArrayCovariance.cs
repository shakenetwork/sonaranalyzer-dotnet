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
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class ArrayCovariance : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2330";
        internal const string Title = "Array covariance should not be used";
        internal const string Description =
            "Array covariance is the principle that if an implicit or explicit reference conversion exits from type \"A\" " +
            "to \"B\", then the same conversion exists from the array type \"A[]\" to \"B[]\". While this array conversion " +
            "can be useful in readonly situations to pass instances of \"A[]\" wherever \"B[]\" is expected, it must be " +
            "used with care, since assigning an instance of \"B\" into an array of \"A\" will cause an " +
            "\"ArrayTypeMismatchException\" to be thrown at runtime.";
        internal const string MessageFormat = "Refactor the code to not rely on potentially unsafe array conversions.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

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
                    var assignment = (AssignmentExpressionSyntax) c.Node;
                    var typeDerived = c.SemanticModel.GetTypeInfo(assignment.Right).Type;
                    var typeBase = c.SemanticModel.GetTypeInfo(assignment.Left).Type;

                    if (AreCovariantArrayTypes(typeDerived, typeBase))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, assignment.Right.GetLocation()));
                    }
                },
                SyntaxKind.SimpleAssignmentExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var variableDeclaration = (VariableDeclarationSyntax)c.Node;
                    var typeBase = c.SemanticModel.GetTypeInfo(variableDeclaration.Type).Type;

                    foreach (var variable in variableDeclaration.Variables
                        .Where(syntax => syntax.Initializer != null))
                    {
                        var typeDerived = c.SemanticModel.GetTypeInfo(variable.Initializer.Value).Type;

                        if (AreCovariantArrayTypes(typeDerived, typeBase))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, variable.Initializer.Value.GetLocation()));
                        }
                    }
                },
                SyntaxKind.VariableDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax)c.Node;
                    var methodParameterLookup = new MethodParameterLookup(invocation, c.SemanticModel);

                    foreach (var argument in invocation.ArgumentList.Arguments)
                    {
                        IParameterSymbol parameter;
                        if (!methodParameterLookup.TryGetParameterSymbol(argument, out parameter) ||
                            parameter.IsParams)
                        {
                            continue;
                        }

                        var typeDerived = c.SemanticModel.GetTypeInfo(argument.Expression).Type;
                        if (AreCovariantArrayTypes(typeDerived, parameter.Type))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation()));
                        }
                    }
                },
                SyntaxKind.InvocationExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var castExpression = (CastExpressionSyntax) c.Node;
                    var typeDerived = c.SemanticModel.GetTypeInfo(castExpression.Expression).Type;
                    var typeBase = c.SemanticModel.GetTypeInfo(castExpression.Type).Type;

                    if (AreCovariantArrayTypes(typeDerived, typeBase))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, castExpression.Type.GetLocation()));
                    }
                },
                SyntaxKind.CastExpression);
        }


        private static bool AreCovariantArrayTypes(ITypeSymbol typeDerivedArray, ITypeSymbol typeBaseArray)
        {
            if (typeDerivedArray == null ||
                typeBaseArray == null ||
                typeBaseArray.Kind != SymbolKind.ArrayType ||
                typeDerivedArray.Kind != SymbolKind.ArrayType)
            {
                return false;
            }

            var typeDerivedElement = ((IArrayTypeSymbol) typeDerivedArray).ElementType;
            var typeBaseElement = ((IArrayTypeSymbol)typeBaseArray).ElementType;

            return typeDerivedElement.BaseType != null &&
                typeDerivedElement.BaseType.ConstructedFrom.DerivesFrom(typeBaseElement);
        }
    }
}
