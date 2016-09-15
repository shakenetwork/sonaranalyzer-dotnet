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
using System.Linq;
using System;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Convention)]
    public class NonFlagsEnumInBitwiseOperation : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3265";
        internal const string Title = "Non-flags enums should not be used in bitwise operations";
        internal const string Description =
            "\"enum\"s are usually used to identify distinct elements in a set of values. However \"enum\"s can be treated as bit fields and bitwise " +
            "operations can be used on them to combine the values. This is a good way of specifying multiple elements of set with a single value. When " +
            "\"enum\"s are used this way, it is a best practice to mark the \"enum\" with the \"FlagsAttribute\".";
        internal const string MessageFormat = "{0}";
        internal const string MessageRemove = "Remove this bitwise operation; the enum \"{0}\" is not marked with \"Flags\" attribute.";
        internal const string MessageChangeOrRemove = "Mark enum \"{0}\" with \"Flags\" attribute or remove this bitwise operation.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Minor;
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
                c => CheckExpressionWithOperator<BinaryExpressionSyntax>(b => b.OperatorToken, c),
                SyntaxKind.BitwiseOrExpression,
                SyntaxKind.BitwiseAndExpression,
                SyntaxKind.ExclusiveOrExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckExpressionWithOperator<AssignmentExpressionSyntax>(a => a.OperatorToken, c),
                SyntaxKind.AndAssignmentExpression,
                SyntaxKind.OrAssignmentExpression,
                SyntaxKind.ExclusiveOrAssignmentExpression);
        }

        private static void CheckExpressionWithOperator<T>(Func<T, SyntaxToken> operatorSelector, SyntaxNodeAnalysisContext context)
            where T : SyntaxNode
        {
            var operation = context.SemanticModel.GetSymbolInfo(context.Node).Symbol as IMethodSymbol;
            if (operation == null ||
                operation.MethodKind != MethodKind.BuiltinOperator ||
                operation.ReturnType == null ||
                operation.ReturnType.TypeKind != TypeKind.Enum)
            {
                return;
            }

            if (!HasFlagsAttribute(operation.ReturnType))
            {
                var friendlyTypeName = operation.ReturnType.ToMinimalDisplayString(context.SemanticModel, context.Node.SpanStart);
                var messageFormat = operation.ReturnType.DeclaringSyntaxReferences.Any()
                    ? MessageChangeOrRemove
                    : MessageRemove;

                var message = string.Format(messageFormat, friendlyTypeName);

                var op = operatorSelector((T)context.Node);
                context.ReportDiagnostic(Diagnostic.Create(Rule, op.GetLocation(), message));
            }
        }

        private static bool HasFlagsAttribute(ISymbol symbol)
        {
            return symbol.GetAttributes().Any(a => a.AttributeClass.Is(KnownType.System_FlagsAttribute));
        }
    }
}
