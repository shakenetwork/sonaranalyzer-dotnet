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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [NoSqaleRemediation]
    [Rule(DiagnosticId, RuleSeverity, Title, true)]
    public class CommentTodo : CommentWordBase
    {
        protected override string Word => "TODO";

        internal const string DiagnosticId = "S1135";
        internal const string Title = "\"TODO\" tags should be handled";
        internal const string Description =
            "\"TODO\" tags are commonly used to mark places where some more code is required, but which the developer " +
            "wants to implement later. Sometimes the developer will not have the time or will simply forget to get back " +
            "to that tag. This rule is meant to track those tags, and ensure that they do not go unnoticed.";
        internal const string MessageFormat =
            "Complete the task associated to this \"TODO\" comment.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Info;

        internal static readonly DiagnosticDescriptor rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), false /*disabled by default in VS not to overlap functionality with the Task List window*/,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        protected override DiagnosticDescriptor Rule => rule;
    }
}
