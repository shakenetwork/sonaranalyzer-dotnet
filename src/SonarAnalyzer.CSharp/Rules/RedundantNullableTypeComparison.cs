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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class RedundantNullableTypeComparison : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3610";
        internal const string Title = "Nullable type comparison should not be redundant";
        internal const string Description =
            "Calling \"GetType()\" on a nullable object returns the underlying value type. Thus, comparing the returned "+
            "\"Type\" object to \"typeof(Nullable<SomeType>)\" doesn't make sense. The comparison either throws an " +
            "exception or the result can be known at compile time.";
        internal const string MessageFormat = "Remove this redundant type comparison.";
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
                    var binary = (BinaryExpressionSyntax)c.Node;
                    CheckGetTypeAndTypeOfEquality(binary.Left, binary.Right, binary.GetLocation(), c);
                    CheckGetTypeAndTypeOfEquality(binary.Right, binary.Left, binary.GetLocation(), c);
                },
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);
        }

        private static void CheckGetTypeAndTypeOfEquality(ExpressionSyntax sideA, ExpressionSyntax sideB, Location location,
            SyntaxNodeAnalysisContext context)
        {
            if (!TypeExaminationOnSystemType.IsGetTypeCall(sideA as InvocationExpressionSyntax, context.SemanticModel))
            {
                return;
            }

            var typeSyntax = (sideB as TypeOfExpressionSyntax)?.Type;
            if (typeSyntax == null)
            {
                return;
            }

            var typeSymbol = context.SemanticModel.GetTypeInfo(typeSyntax).Type;
            if (typeSymbol != null &&
                typeSymbol.OriginalDefinition.Is(KnownType.System_Nullable_T))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        }
    }
}
