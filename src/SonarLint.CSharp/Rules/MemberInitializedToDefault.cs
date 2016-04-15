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
    [Tags(Tag.Finding)]
    public class MemberInitializedToDefault : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3052";
        internal const string Title = "Members should not be initialized to default values";
        internal const string Description =
            "The compiler automatically initializes class fields, auto-properties and events to their default values before setting them with any " +
            "initialization values, so there is no need to explicitly set a member to its default value. Further, under the logic that cleaner code " +
            "is better code, it's considered poor style to do so.";
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
                c => CheckField(c),
                SyntaxKind.FieldDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckEvent(c),
                SyntaxKind.EventFieldDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckAutoProperty(c),
                SyntaxKind.PropertyDeclaration);
        }

        private static void CheckAutoProperty(SyntaxNodeAnalysisContext context)
        {
            var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;

            if (propertyDeclaration.Initializer == null ||
                propertyDeclaration.AccessorList == null ||
                propertyDeclaration.AccessorList.Accessors.Any(Accessibility => Accessibility.Body != null))
            {
                return;
            }

            var propertySymbol = context.SemanticModel.GetDeclaredSymbol(propertyDeclaration) as IPropertySymbol;

            if (propertySymbol != null &&
                IsDefaultValueInitializer(propertyDeclaration.Initializer, propertySymbol.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, propertyDeclaration.Initializer.GetLocation(), propertySymbol.Name));
                return;
            }
        }

        private static void CheckEvent(SyntaxNodeAnalysisContext context)
        {
            var field = (EventFieldDeclarationSyntax)context.Node;

            foreach (var eventDeclaration in field.Declaration.Variables.Where(v => v.Initializer != null))
            {
                var eventSymbol = context.SemanticModel.GetDeclaredSymbol(eventDeclaration) as IEventSymbol;
                if (eventSymbol == null)
                {
                    continue;
                }

                if (IsDefaultValueInitializer(eventDeclaration.Initializer, eventSymbol.Type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, eventDeclaration.Initializer.GetLocation(), eventSymbol.Name));
                    return;
                }
            }
        }

        private static void CheckField(SyntaxNodeAnalysisContext context)
        {
            var field = (FieldDeclarationSyntax)context.Node;

            foreach (var variableDeclarator in field.Declaration.Variables.Where(v => v.Initializer != null))
            {
                var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variableDeclarator) as IFieldSymbol;

                if (fieldSymbol != null &&
                    !fieldSymbol.IsConst &&
                    IsDefaultValueInitializer(variableDeclarator.Initializer, fieldSymbol.Type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, variableDeclarator.Initializer.GetLocation(), fieldSymbol.Name));
                }
            }
        }

        private static bool IsDefaultValueInitializer(EqualsValueClauseSyntax initializer, ITypeSymbol type)
        {
            return CheckDefaultExpressionInitializer(initializer) ||
                CheckReferenceTypeNullInitializer(initializer, type) ||
                CheckValueTypeDefaultValueInitializer(initializer, type);
        }

        private static bool CheckDefaultExpressionInitializer(EqualsValueClauseSyntax initializer)
        {
            var defaultValue = initializer.Value as DefaultExpressionSyntax;
            return defaultValue != null;
        }

        private static bool CheckReferenceTypeNullInitializer(EqualsValueClauseSyntax initializer, ITypeSymbol type)
        {
            return type.IsReferenceType &&
                EquivalenceChecker.AreEquivalent(NullExpression, initializer.Value);
        }

        private static bool CheckValueTypeDefaultValueInitializer(EqualsValueClauseSyntax initializer, ITypeSymbol type)
        {
            if (!type.IsValueType)
            {
                return false;
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return EquivalenceChecker.AreEquivalent(initializer.Value, FalseExpression);
                case SpecialType.System_Decimal:
                case SpecialType.System_Double:
                case SpecialType.System_Single:
                    {
                        double constantValue;
                        return ExpressionNumericConverter.TryGetConstantDoubleValue(initializer.Value, out constantValue) &&
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
                        return ExpressionNumericConverter.TryGetConstantIntValue(initializer.Value, out constantValue) &&
                            constantValue == default(int);
                    }
                default:
                    return false;
            }
        }
    }
}
