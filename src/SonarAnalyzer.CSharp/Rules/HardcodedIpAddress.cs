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

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    [SqaleConstantRemediation("30min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureChangeability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cert, Tag.Security)]
    public class HardcodedIpAddress : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1313";
        internal const string Title = "IP addresses should not be hardcoded";
        internal const string Description =
            "Hardcoding an IP address into source code is a bad idea for several reasons: " +
            "a recompile is required if the address changes; it forces the same address to be " +
            "used in every environment (dev, sys, qa, prod); it places the responsibility of " +
            "setting the value to use in production on the shoulders of the developer; it " +
            "allows attackers to decompile the code and thereby discover a potentially " +
            "sensitive address";
        internal const string MessageFormat = "Make this IP \"{0}\" address configurable.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly string[] SkippedWords = {"VERSION", "ASSEMBLY"};
        private static readonly Type[] NodeTypesToCheck = {typeof(StatementSyntax), typeof(VariableDeclaratorSyntax), typeof(ParameterSyntax) };

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var stringLiteral = (LiteralExpressionSyntax)c.Node;
                    var text = stringLiteral.Token.ValueText;

                    if (text == "::")
                    {
                        return;
                    }

                    IPAddress address;
                    if (!IPAddress.TryParse(text, out address))
                    {
                        return;
                    }

                    if (address.AddressFamily == AddressFamily.InterNetwork &&
                        text.Split('.').Length != 4)
                    {
                        return;
                    }

                    foreach (var type in NodeTypesToCheck)
                    {
                        var ancestorOrSelf = stringLiteral.FirstAncestorOrSelf<SyntaxNode>(type.IsInstanceOfType);
                        if (ancestorOrSelf != null && SkippedWords.Any(s => ancestorOrSelf.ToString().ToUpperInvariant().Contains(s)))
                        {
                            return;
                        }
                    }

                    var attribute = stringLiteral.FirstAncestorOrSelf<AttributeSyntax>();
                    if (attribute != null)
                    {
                        return;
                    }

                    c.ReportDiagnostic(Diagnostic.Create(Rule, stringLiteral.GetLocation(), text));
                },
                SyntaxKind.StringLiteralExpression);
        }
    }
}
