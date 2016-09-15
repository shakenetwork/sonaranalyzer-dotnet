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
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System.Linq;
using System;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, true)]
    [Tags(Tag.Convention)]
    public class LocalVariableName : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S117";
        internal const string Title = "Method parameters should follow a naming convention";
        internal const string Description =
            "Shared naming conventions allow teams to collaborate efficiently. " +
            "This rule checks that all method parameters follow a naming convention.";
        internal const string MessageFormat = "Rename this local variable to match the regular expression: \"{0}\".";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), false,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        [RuleParameter("format", PropertyType.String,
            "Regular expression used to check the local variable names against.", FieldNameChecker.CamelCasingPattern)]
        public string Pattern { get; set; } = FieldNameChecker.CamelCasingPattern;

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ProcessVariableDeclarator(c),
                SyntaxKind.VariableDeclarator);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ProcessLoop((ForStatementSyntax)c.Node, f => f.ControlVariable, s => s.IsFor(), c),
                SyntaxKind.ForStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => ProcessLoop((ForEachStatementSyntax)c.Node, f => f.ControlVariable, s => s.IsForEach(), c),
                SyntaxKind.ForEachStatement);
        }

        private void ProcessLoop<T>(T loop, Func<T, VisualBasicSyntaxNode> GetControlVariable, Func<ILocalSymbol, bool> isDeclaredInLoop,
            SyntaxNodeAnalysisContext context)
        {
            var controlVar = GetControlVariable(loop);
            if (!(controlVar is IdentifierNameSyntax))
            {
                return;
            }

            var symbol = context.SemanticModel.GetSymbolInfo(controlVar).Symbol as ILocalSymbol;
            if (symbol == null ||
                !isDeclaredInLoop(symbol) ||
                FieldNameChecker.IsRegexMatch(symbol.Name, Pattern))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, controlVar.GetLocation(), Pattern));
        }

        private void ProcessVariableDeclarator(SyntaxNodeAnalysisContext context)
        {
            var declarator = (VariableDeclaratorSyntax)context.Node;
            if (declarator.Parent is FieldDeclarationSyntax)
            {
                return;
            }

            foreach (var name in declarator.Names
                .Where(n => n != null &&
                    !FieldNameChecker.IsRegexMatch(n.Identifier.ValueText, Pattern)))
            {
                var symbol = context.SemanticModel.GetDeclaredSymbol(name) as ILocalSymbol;
                if (symbol == null ||
                    symbol.IsConst)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(Rule, name.Identifier.GetLocation(), Pattern));
            }
        }
    }
}
