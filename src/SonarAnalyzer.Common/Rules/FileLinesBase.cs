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
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using System.Linq;

namespace SonarAnalyzer.Rules
{
    public abstract class FileLinesBase : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S104";
        internal const string Title = "Files should not have too many lines";
        internal const string Description =
            "A source file that grows too much tends to aggregate too many responsibilities and inevitably becomes harder to understand and " +
            "therefore to maintain. Above a specific threshold, it is strongly advised to refactor it into smaller pieces of code which focus " +
            "on well defined tasks. Those smaller files will not only be easier to understand but also probably easier to test.";
        internal const string MessageFormat = "This file has {1} lines, which is greater than {0} authorized. Split it into smaller files.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), false,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const int DefaultValueMaximum = 1000;

        [RuleParameter("maximumFileLocThreshold", PropertyType.Integer, "Maximum authorized lines in a file.", DefaultValueMaximum)]
        public int Maximum { get; set; } = DefaultValueMaximum;

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var root = c.Tree.GetRoot();
                    var lines = root.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

                    if (lines > Maximum)
                    {
                        var firstLine = c.Tree.GetText().Lines.First();
                        c.ReportDiagnostic(Diagnostic.Create(Rule, c.Tree.GetLocation(firstLine.Span), Maximum, lines));
                    }
                });
        }
    }
}
