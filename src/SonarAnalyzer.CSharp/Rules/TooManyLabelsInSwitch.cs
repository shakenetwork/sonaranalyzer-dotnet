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

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataChangeability)]
    [SqaleConstantRemediation("30min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.BrainOverload)]
    public class TooManyLabelsInSwitch : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1479";
        internal const string Title = "\"switch\" statements should not have too many \"case\" clauses";
        internal const string Description =
            "When \"switch\" statements have a large set of \"case\" clauses, it is usually an attempt to map two sets of data. A real map structure " +
            "would be more readable and maintainable, and should be used instead.";
        internal const string MessageFormat = "Consider reworking this \"switch\" to reduce the number of \"case\"s from {1} to at most {0}.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const int DefaultValueMaximum = 30;

        [RuleParameter("maximum", PropertyType.Integer, "Maximum number of case", DefaultValueMaximum)]
        public int Maximum { get; set; } = DefaultValueMaximum;

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var switchNode = (SwitchStatementSyntax)c.Node;
                    var type = c.SemanticModel.GetTypeInfo(switchNode.Expression).Type;

                    if (type == null ||
                        type.TypeKind == TypeKind.Enum)
                    {
                        return;
                    }

                    var labels = NumberOfLabels(switchNode);

                    if (labels > Maximum)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, switchNode.SwitchKeyword.GetLocation(), Maximum, labels));
                    }
                },
                SyntaxKind.SwitchStatement);
        }

        private static int NumberOfLabels(SwitchStatementSyntax node)
        {
            return node.Sections.Sum(e => e.Labels.Count);
        }
    }
}
