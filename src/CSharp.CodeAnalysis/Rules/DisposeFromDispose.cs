/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
using SonarQube.CSharp.CodeAnalysis.Common;
using SonarQube.CSharp.CodeAnalysis.Common.Sqale;
using SonarQube.CSharp.CodeAnalysis.Helpers;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("bug", "cwe", "denial-of-service", "security")]
    public class DisposeFromDispose : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2952";
        internal const string Title = "Classes should \"Dispose\" of members from the classes' own \"Dispose\" methods";
        internal const string Description =
            "It is possible in an \"IDisposable\" to call \"Dispose\" on class members from any method, but the contract of " +
            "\"Dispose\" is that it will clean up all unmanaged resources. Move disposing of members to some other method, " +
            "and you risk resource leaks.";
        internal const string MessageFormat = "Move this \"Dispose\" call into this class' own \"Dispose\" method.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        private const string DisposeMethodName = "Dispose";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax) c.Node;
                    var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
                    if (memberAccess == null)
                    {
                        return;
                    }

                    var fieldSymbol = c.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol as IFieldSymbol;
                    if (fieldSymbol == null ||
                        !DisposableMemberInNonDisposableClass.IsNonStaticNonPublicDisposableField(fieldSymbol) ||
                        !DisposableMemberInNonDisposableClass.ImplementsIDisposable(fieldSymbol.ContainingType))
                    {
                        return;
                    }

                    var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (methodSymbol == null)
                    {
                        return;
                    }

                    var disposeMethod =
                        (IMethodSymbol) c.SemanticModel.Compilation.GetSpecialType(SpecialType.System_IDisposable)
                            .GetMembers(DisposeMethodName)
                            .Single();

                    if (!methodSymbol.Equals(
                            methodSymbol.ContainingType.FindImplementationForInterfaceMember(disposeMethod)))
                    {
                        return;
                    }

                    var enclosingSymbol = c.SemanticModel.GetEnclosingSymbol(invocation.SpanStart);
                    if (enclosingSymbol == null)
                    {
                        return;
                    }

                    var enclosingMethodSymbol = enclosingSymbol as IMethodSymbol;
                    if (enclosingMethodSymbol == null ||
                        enclosingMethodSymbol.Name != DisposeMethodName)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation()));
                    }
                },
                SyntaxKind.InvocationExpression);
        }
    }
}
