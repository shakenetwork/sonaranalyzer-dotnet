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

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious)]
    public class ForeachLoopExplicitConversion : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3217";
        internal const string Title = "\"Explicit\" conversions of \"foreach\" loops should not be used";
        internal const string Description =
            "The \"foreach\" statement was introduced in the C# language prior to generics. To make it easier to work with non-generic " +
            "collections available at that time such as \"ArrayList\", the \"foreach\" statements allows to downcast the collection's " +
            "element of type \"Object\" into any other type. The problem is that, to achieve that, the \"foreach\" statements silently " +
            "performs \"explicit\" type conversion, which at runtime can result in an \"InvalidCastException\" to be thrown. C# code " +
            "iterating on generic collections or arrays should not rely on \"foreach\" statement's silent \"explicit\" conversions.";
        internal const string MessageFormat = "Either change the type of \"{0}\" to \"{1}\" or iterate on a generic collection of type \"{2}\".";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
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
                    var foreachStatement = (ForEachStatementSyntax)c.Node;
                    var foreachInfo = c.SemanticModel.GetForEachStatementInfo(foreachStatement);

                    if (foreachInfo.Equals(default(ForEachStatementInfo)) ||
                        foreachInfo.ElementConversion.IsImplicit ||
                        foreachInfo.ElementConversion.IsUserDefined ||
                        !foreachInfo.ElementConversion.Exists ||
                        foreachInfo.ElementType.Is(KnownType.System_Object))
                    {
                        return;
                    }

                    c.ReportDiagnostic(Diagnostic.Create(Rule, foreachStatement.Type.GetLocation(),
                        foreachStatement.Identifier.ValueText,
                        foreachInfo.ElementType.ToDisplayString(),
                        foreachStatement.Type.ToString()));
                },
                SyntaxKind.ForEachStatement);
        }
    }
}
