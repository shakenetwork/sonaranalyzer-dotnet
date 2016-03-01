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
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Cwe, Tag.SansTop25Risky, Tag.Security)]
    public class LossOfFractionInDivision : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2184";
        internal const string Title = "Result of integer division should not be assigned to floating point variable";
        internal const string Description =
            "When division is performed on \"int\"s, the result will always be an \"int\". You can assign that result to a \"double\", " +
            "\"float\" or \"decimal\" with automatic type conversion, but having started as an \"int\", the result will likely not be " +
            "what you expect. If the result of \"int\" division is assigned to a floating-point variable, precision will have been " +
            "lost before the assignment. Instead, at least one operand should be cast or promoted to the final type before the " +
            "operation takes place.";
        internal const string MessageFormat = "Cast one of the operands of this division to \"{0}\".";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var division = (BinaryExpressionSyntax) c.Node;

                    var symbol = c.SemanticModel.GetSymbolInfo(division).Symbol as IMethodSymbol;
                    if (symbol == null ||
                        symbol.ContainingType == null ||
                        !symbol.ContainingType.IsAny(KnownType.IntegralNumbers))
                    {
                        return;
                    }

                    ITypeSymbol assignedToType;
                    if (TryGetTypeFromAssignmentToFloatType(division, c.SemanticModel, out assignedToType) ||
                        TryGetTypeFromArgumentMappedToFloatType(division, c.SemanticModel, out assignedToType) ||
                        TryGetTypeFromReturnMappedToFloatType(division, c.SemanticModel, out assignedToType))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(
                            Rule,
                            division.GetLocation(),
                            assignedToType.ToMinimalDisplayString(c.SemanticModel, division.SpanStart)));
                    }
                },
                SyntaxKind.DivideExpression);
        }

        private static bool TryGetTypeFromReturnMappedToFloatType(BinaryExpressionSyntax division, SemanticModel semanticModel,
            out ITypeSymbol type)
        {
            if (division.Parent is ReturnStatementSyntax ||
                division.Parent is LambdaExpressionSyntax)
            {
                type = (semanticModel.GetEnclosingSymbol(division.SpanStart) as IMethodSymbol)?.ReturnType;
                return type.IsAny(KnownType.NonIntegralNumbers);
            }

            type = null;
            return false;
        }

        private static bool TryGetTypeFromArgumentMappedToFloatType(BinaryExpressionSyntax division, SemanticModel semanticModel,
            out ITypeSymbol type)
        {
            var argument = division.Parent as ArgumentSyntax;
            if (argument == null)
            {
                type = null;
                return false;
            }

            var invocation = argument.Parent.Parent as InvocationExpressionSyntax;
            if (invocation == null)
            {
                type = null;
                return false;
            }

            var lookup = new MethodParameterLookup(invocation, semanticModel);
            IParameterSymbol parameter;
            if (!lookup.TryGetParameterSymbol(argument, out parameter))
            {
                type = null;
                return false;
            }

            type = parameter.Type;
            return type.IsAny(KnownType.NonIntegralNumbers);
        }

        private static bool TryGetTypeFromAssignmentToFloatType(BinaryExpressionSyntax division, SemanticModel semanticModel,
            out ITypeSymbol type)
        {
            var assignment = division.Parent as AssignmentExpressionSyntax;
            if (assignment != null)
            {
                type = semanticModel.GetTypeInfo(assignment.Left).Type;
                return type.IsAny(KnownType.NonIntegralNumbers);
            }

            var variableDecl = division.Parent.Parent.Parent as VariableDeclarationSyntax;
            if (variableDecl != null)
            {
                type = semanticModel.GetTypeInfo(variableDecl.Type).Type;
                return type.IsAny(KnownType.NonIntegralNumbers);
            }

            type = null;
            return false;
        }
    }
}
