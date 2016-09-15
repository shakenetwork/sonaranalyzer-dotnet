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
using SonarAnalyzer.Helpers.CSharp;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class GenericTypeParameterEmptinessChecking : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2955";
        internal const string Title = "Generic parameters not constrained to reference types should not be compared to \"null\"";
        internal const string Description =
            "When constraints have not been applied to restrict a generic type parameter to be " +
            "a reference type, then a value type, such as a \"struct\", could also be passed. " +
            "In such cases, comparing the type parameter to \"null\" would always be false, " +
            "because a \"struct\" can be empty, but never \"null\". If a value type is truly " +
            "what's expected, then the comparison should use \"default()\". If it's not, then " +
            "constraints should be added so that no value type can be passed.";
        internal const string MessageFormat =
            "Use a comparison to \"default({0})\" instead or add a constraint to \"{0}\" so that it can't be a value type.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
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
                    var equalsExpression = (BinaryExpressionSyntax) c.Node;

                    var leftIsNull = EquivalenceChecker.AreEquivalent(equalsExpression.Left, SyntaxHelper.NullLiteralExpression);
                    var rightIsNull = EquivalenceChecker.AreEquivalent(equalsExpression.Right, SyntaxHelper.NullLiteralExpression);

                    if (!(leftIsNull ^ rightIsNull))
                    {
                        return;
                    }

                    var expressionToTypeCheck = leftIsNull ? equalsExpression.Right : equalsExpression.Left;
                    var typeInfo = c.SemanticModel.GetTypeInfo(expressionToTypeCheck).Type as ITypeParameterSymbol;
                    if (typeInfo != null &&
                        !typeInfo.HasReferenceTypeConstraint &&
                        !typeInfo.ConstraintTypes.OfType<IErrorTypeSymbol>().Any() &&
                        !typeInfo.ConstraintTypes.Any(typeSymbol =>
                            typeSymbol.IsReferenceType &&
                            typeSymbol.IsClass()))
                    {
                        var expressionToReportOn = leftIsNull ? equalsExpression.Left : equalsExpression.Right;

                        c.ReportDiagnostic(Diagnostic.Create(Rule, expressionToReportOn.GetLocation(),
                            typeInfo.Name));
                    }
                },
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);
        }
    }
}
