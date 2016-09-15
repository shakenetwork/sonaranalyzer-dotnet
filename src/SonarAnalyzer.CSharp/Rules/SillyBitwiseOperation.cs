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
using Microsoft.CodeAnalysis.Text;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious)]
    public class SillyBitwiseOperation : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2437";
        internal const string Title = "Silly bit operations should not be performed";
        internal const string Description =
            "Certain bit operations are just silly and should not be performed because their results are predictable. " +
            "Specifically, using \"& -1\" with any value will always result in the original value, as will \"anyValue ^ 0\" " +
            "and \"anyValue | 0\".";
        internal const string MessageFormat = "Remove this silly bit operation.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        internal const string IsReportingOnLeftKey = "IsReportingOnLeft";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckBinary(c, -1),
                SyntaxKind.BitwiseAndExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckBinary(c, 0),
                SyntaxKind.BitwiseOrExpression,
                SyntaxKind.ExclusiveOrExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckAssignment(c, -1),
                SyntaxKind.AndAssignmentExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckAssignment(c, 0),
                SyntaxKind.OrAssignmentExpression,
                SyntaxKind.ExclusiveOrAssignmentExpression);
        }

        private static void CheckAssignment(SyntaxNodeAnalysisContext context, int constValueToLookFor)
        {
            var assignment = (AssignmentExpressionSyntax)context.Node;
            int constValue;
            if (ExpressionNumericConverter.TryGetConstantIntValue(assignment.Right, out constValue) &&
                constValue == constValueToLookFor)
            {
                var location = assignment.Parent is StatementSyntax
                    ? assignment.Parent.GetLocation()
                    : GetReportLocation(assignment.OperatorToken.Span, assignment.Right.Span, assignment.SyntaxTree);
                context.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        }

        private static void CheckBinary(SyntaxNodeAnalysisContext context, int constValueToLookFor)
        {
            var binary = (BinaryExpressionSyntax) context.Node;
            int constValue;
            if (ExpressionNumericConverter.TryGetConstantIntValue(binary.Left, out constValue) &&
                constValue == constValueToLookFor)
            {
                var location = GetReportLocation(binary.Left.Span, binary.OperatorToken.Span, binary.SyntaxTree);
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, ImmutableDictionary<string, string>.Empty.Add(IsReportingOnLeftKey, true.ToString())));
                return;
            }

            if (ExpressionNumericConverter.TryGetConstantIntValue(binary.Right, out constValue) &&
                constValue == constValueToLookFor)
            {
                var location = GetReportLocation(binary.OperatorToken.Span, binary.Right.Span, binary.SyntaxTree);
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, ImmutableDictionary<string, string>.Empty.Add(IsReportingOnLeftKey, false.ToString())));
            }
        }

        private static Location GetReportLocation(TextSpan start, TextSpan end, SyntaxTree tree)
        {
            return Location.Create(tree, new TextSpan(start.Start, end.End - start.Start));
        }
    }
}
