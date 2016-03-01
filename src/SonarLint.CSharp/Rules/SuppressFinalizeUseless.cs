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

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Confusing, Tag.Unused)]
    public class SuppressFinalizeUseless : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3234";
        internal const string Title = "\"GC.SuppressFinalize\" should not be invoked for types without destructors";
        internal const string Description =
            "\"GC.SuppressFinalize\" asks the Common Language Runtime not to call the finalizer of an object. This is useful when " +
            "implementing the dispose pattern where object finalization is already handled in \"IDisposable.Dispose\". However, it " +
            "has no effect if there is no finalizer defined in the object's type, so using it in such cases is just confusing.";
        internal const string MessageFormat = "Remove this useless call to \"GC.SuppressFinalize\".";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax)c.Node;
                    var suppressFinalizeSymbol = c.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;

                    if (suppressFinalizeSymbol?.Name != "SuppressFinalize" ||
                        !invocation.HasExactlyNArguments(1) ||
                        !suppressFinalizeSymbol.IsInType(KnownType.System_GC))
                    {
                        return;
                    }

                    var argument = invocation.ArgumentList.Arguments.First();
                    var argumentType = c.SemanticModel.GetTypeInfo(argument.Expression).Type as INamedTypeSymbol;

                    if (!argumentType.IsClass() ||
                        !argumentType.IsSealed)
                    {
                        return;
                    }

                    var hasFinalizer = argumentType.GetSelfAndBaseTypes()
                        .Where(type => !type.Is(KnownType.System_Object))
                        .SelectMany(type => type.GetMembers())
                        .OfType<IMethodSymbol>()
                        .Any(methodSymbol => methodSymbol.MethodKind == MethodKind.Destructor);

                    if (!hasFinalizer)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
                    }
                },
                SyntaxKind.InvocationExpression);
        }
    }
}
