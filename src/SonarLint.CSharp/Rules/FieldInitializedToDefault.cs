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
using System;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    [Tags(Tag.Convention, Tag.Finding)]
    public class FieldInitializedToDefault : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3052";
        internal const string Title = "Fields should not be initialized to default values";
        internal const string Description =
            "The compiler automatically initializes class fields to their default values before setting them with any " +
            "initialization values, so there is no need to explicitly set a field to its default value, but doing. "+
            "Further, under the logic that cleaner code is better code, it's considered poor style to do so.";
        internal const string MessageFormat = "Remove this initialization to \"{0}\", the compiler will do that for you.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), true,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly ExpressionSyntax NullExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
        private static readonly ExpressionSyntax FalseExpression = SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var field = (FieldDeclarationSyntax)c.Node;

                    foreach (var variable in field.Declaration.Variables
                        .Where(v => v.Initializer != null))
                    {
                        var variableSymbol = c.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                        if (variableSymbol == null ||
                            variableSymbol.IsConst)
                        {
                            continue;
                        }

                        if (CheckDefaultExpressionInitializer(variable) ||
                            CheckReferenceTypeNullInitializer(variable, variableSymbol) ||
                            CheckValueTypeDefaultValueInitializer(variable, variableSymbol))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, variable.Initializer.GetLocation(), variableSymbol.Name));
                            return;
                        }
                    }
                },
                SyntaxKind.FieldDeclaration);
        }

        private static bool CheckDefaultExpressionInitializer(VariableDeclaratorSyntax variable)
        {
            var defaultValue = variable.Initializer.Value as DefaultExpressionSyntax;
            return defaultValue != null;
        }

        private static bool CheckReferenceTypeNullInitializer(VariableDeclaratorSyntax variable, IFieldSymbol variableSymbol)
        {
            return variableSymbol.Type.IsReferenceType &&
                EquivalenceChecker.AreEquivalent(NullExpression, variable.Initializer.Value);
        }

        private static bool CheckValueTypeDefaultValueInitializer(VariableDeclaratorSyntax variable, IFieldSymbol variableSymbol)
        {
            if (!variableSymbol.Type.IsValueType)
            {
                return false;
            }

            switch (variableSymbol.Type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return EquivalenceChecker.AreEquivalent(variable.Initializer.Value, FalseExpression);
                case SpecialType.System_Decimal:
                case SpecialType.System_Double:
                case SpecialType.System_Single:
                    {
                        double constantValue;
                        return ExpressionNumericConverter.TryGetConstantDoubleValue(variable.Initializer.Value, out constantValue) &&
                            Math.Abs(constantValue - default(double)) < double.Epsilon;
                    }
                case SpecialType.System_Char:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_SByte:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                    {
                        int constantValue;
                        return ExpressionNumericConverter.TryGetConstantIntValue(variable.Initializer.Value, out constantValue) &&
                            constantValue == default(int);
                    }
                default:
                    return false;
            }
        }
    }
}
