/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
using Microsoft.CodeAnalysis.Text;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("convention")]
    public class FieldInitializedToDefault : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3052";
        internal const string Title = "Fields should not be initialized to default values";
        internal const string Description =
            "The compiler automatically initializes class fields to their default values before setting them with any " +
            "initialization values, so there is no need to explicitly set a field to its default value, but doing. "+
            "Further, under the logic that cleaner code is better code, it's considered poor style to do so.";
        internal const string MessageFormat = "Remove this initialization to \"{0}\", the compiler will do that for you.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly ExpressionSyntax NullExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
        private static readonly ExpressionSyntax FalseExpression = SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var field = (FieldDeclarationSyntax)c.Node;

                    foreach (var variable in field.Declaration.Variables
                        .Where(v => v.Initializer != null))
                    {
                        var variableSymbol = c.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                        if (variableSymbol == null)
                        {
                            continue;
                        }

                        if (CheckDefaultExpressionInitializer(variable, variableSymbol, c.SemanticModel) ||
                            CheckReferenceTypeNullInitializer(variable, variableSymbol) ||
                            CheckValueTypeDefaultValueInitializer(variable, variableSymbol, c.SemanticModel))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, variable.Initializer.GetLocation(), variableSymbol.Name));
                            return;
                        }
                    }
                },
                SyntaxKind.FieldDeclaration);
        }

        private static bool CheckDefaultExpressionInitializer(VariableDeclaratorSyntax variable, IFieldSymbol variableSymbol,
            SemanticModel semanticModel)
        {
            var defaultValue = variable.Initializer.Value as DefaultExpressionSyntax;
            return defaultValue != null;
        }

        private static bool CheckReferenceTypeNullInitializer(VariableDeclaratorSyntax variable, IFieldSymbol variableSymbol)
        {
            return variableSymbol.Type.IsReferenceType &&
                EquivalenceChecker.AreEquivalent(NullExpression, variable.Initializer.Value);
        }

        private static bool CheckValueTypeDefaultValueInitializer(VariableDeclaratorSyntax variable, IFieldSymbol variableSymbol,
            SemanticModel semanticModel)
        {
            if (!variableSymbol.Type.IsValueType)
            {
                return false;
            }

            switch (variableSymbol.Type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return EquivalenceChecker.AreEquivalent(variable.Initializer.Value, FalseExpression);
                case SpecialType.System_Char:
                case SpecialType.System_Byte:
                case SpecialType.System_Decimal:
                case SpecialType.System_Double:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_SByte:
                case SpecialType.System_Single:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                    int constantValue;
                    return SillyBitwiseOperation.TryGetConstantIntValue(variable.Initializer.Value, out constantValue) &&
                        constantValue == 0;
                default:
                    return false;
            }
        }
    }
}
