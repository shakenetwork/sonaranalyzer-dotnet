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
using System.Collections.Generic;
using System;

namespace SonarAnalyzer.Rules.CSharp
{
    using CamelCaseConverter = ClassName.CamelCaseConverter;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Convention)]
    public class MethodName : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S100";
        internal const string Title = "Methods and properties should be named in camel case";
        internal const string Description =
            "Shared naming conventions allow teams to collaborate efficiently. This rule checks whether or not method and property names are camel cased.";
        internal const string MessageFormat = "Rename {0} \"{1}\" to match camel case naming rules, {2}.";
        internal const string MessageFormatNonUnderscore = "consider using \"{0}\"";
        internal const string MessageFormatUnderscore = "trim underscores from the name";
        internal const string Category = SonarAnalyzer.Common.Category.Naming;
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
                c =>
                {
                    var declaration = (MethodDeclarationSyntax)c.Node;
                    CheckDeclarationName(declaration, declaration.Identifier, c);
                },
                SyntaxKind.MethodDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (PropertyDeclarationSyntax)c.Node;
                    CheckDeclarationName(declaration, declaration.Identifier, c);
                },
                SyntaxKind.PropertyDeclaration);
        }

        private static void CheckDeclarationName(MemberDeclarationSyntax member, SyntaxToken identifier, SyntaxNodeAnalysisContext context)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(member);
            if (symbol == null)
            {
                return;
            }

            if (ClassName.IsTypeComRelated(symbol.ContainingType) ||
                symbol.IsInterfaceImplementationOrMemberOverride() ||
                symbol.IsExtern)
            {
                return;
            }

            if (identifier.ValueText.StartsWith("_", StringComparison.Ordinal) ||
                identifier.ValueText.EndsWith("_", StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), MethodKindNameMapping[member.Kind()],
                    identifier.ValueText, MessageFormatUnderscore));
                return;
            }

            string suggestion;
            if (TryGetChangedName(identifier.ValueText, out suggestion))
            {
                var messageEnding = string.Format(MessageFormatNonUnderscore, suggestion);
                context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), MethodKindNameMapping[member.Kind()],
                    identifier.ValueText, messageEnding));
            }
        }

        private static bool TryGetChangedName(string identifierName, out string suggestion)
        {
            if (identifierName.Contains('_'))
            {
                suggestion = null;
                return false;
            }

            suggestion = CamelCaseConverter.Convert(identifierName);
            return identifierName != suggestion;
        }

        private static readonly Dictionary<SyntaxKind, string> MethodKindNameMapping = new Dictionary<SyntaxKind, string>
        {
            {SyntaxKind.MethodDeclaration, "method" },
            {SyntaxKind.PropertyDeclaration, "property" }
        };
    }
}
