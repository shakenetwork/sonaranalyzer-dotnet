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
using System.Linq;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Convention)]
    public class FrameworkTypeNaming : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3376";
        internal const string Title = "Attribute, EventArgs, and Exception type names should end with the type being extended";
        internal const string Description =
            "Adherence to the standard naming conventions makes your code not only more readable, but more usable. " +
            "For instance, \"class FirstAttribute : Attribute\" can be used simply with \"First\", but you must use the " +
            "full name for \"class AttributeOne : Attribute\". This rule raises an issue when classes extending " +
            "\"Attribute\", \"EventArgs\", or \"Exception\", do not end with their parent class names.";
        internal const string MessageFormat = "Make this class name end with \"{0}\".";
        internal const string Category = SonarLint.Common.Category.Naming;
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
                    var classDeclaration = (ClassDeclarationSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(classDeclaration);
                    if (symbol == null)
                    {
                        return;
                    }

                    var baseTypes = symbol.BaseType.GetSelfAndBaseTypes().ToList();

                    if (baseTypes.Count < 2 ||
                        !baseTypes.Last().Is(KnownType.System_Object))
                    {
                        return;
                    }

                    var baseTypeKey = FrameworkTypesWithEnding.Keys
                        .FirstOrDefault(ft => baseTypes[baseTypes.Count-2].ToDisplayString().Equals(ft, System.StringComparison.Ordinal));

                    if (baseTypeKey == null)
                    {
                        return;
                    }

                    var baseTypeName = FrameworkTypesWithEnding[baseTypeKey];

                    if (symbol.Name.EndsWith(baseTypeName, System.StringComparison.Ordinal) ||
                        !baseTypes[0].Name.EndsWith(baseTypeName, System.StringComparison.Ordinal))
                    {
                        return;
                    }

                    c.ReportDiagnostic(Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(), baseTypeName));
                },
                SyntaxKind.ClassDeclaration);
        }

        private static readonly Dictionary<string, string> FrameworkTypesWithEnding = new Dictionary<string, string>
        {
            { "System.Exception", "Exception" },
            { "System.EventArgs", "EventArgs" },
            { "System.Attribute", "Attribute" }
        };
    }
}
