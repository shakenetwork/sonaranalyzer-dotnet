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

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class PureAttributeOnVoidMethod : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3603";
        internal const string Title = "Methods with \"Pure\" attribute should return a value";
        internal const string Description =
            "Marking a method with the \"[Pure]\" attribute specifies that the method doesn't " +
            "make any visible changes; thus, the method should return a result, otherwise the " +
            "call to the method should be equal to no-operation. So \"[Pure]\" on a \"void\" method " +
            "is either a mistake, or the method doesn't do any meaningful task.";
        internal const string MessageFormat = "Remove the \"Pure\" attribute or change the method to return a value.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
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
                    var methodDeclaration = (MethodDeclarationSyntax) c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(methodDeclaration);
                    if (methodSymbol == null ||
                        !methodSymbol.ReturnsVoid||
                        methodSymbol.Parameters.Any(p => p.RefKind != RefKind.None))
                    {
                        return;
                    }

                    AttributeSyntax pureAttribute;
                    if (methodDeclaration.AttributeLists.TryGetAttribute(KnownType.System_Diagnostics_Contracts_PureAttribute,
                            c.SemanticModel, out pureAttribute))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, pureAttribute.GetLocation()));
                    }
                },
                SyntaxKind.MethodDeclaration);
        }
    }
}
