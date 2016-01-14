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

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Cwe, Tag.DenialOfService, Tag.Security)]
    public class DisposableNotDisposed : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2930";
        internal const string Title = "\"IDisposables\" should be disposed";
        internal const string MessageFormat = "\"Dispose\" of \"{0}\".";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly IImmutableSet<string> TrackedTypes = new []
        {
            "System.IO.FileStream",
            "System.IO.StreamReader",
            "System.IO.StreamWriter",

            "System.Net.WebClient",

            "System.Net.Sockets.TcpClient",
            "System.Net.Sockets.TcpListener",
            "System.Net.Sockets.UdpClient",

            "System.Drawing.Image",
            "System.Drawing.Bitmap"
        }.ToImmutableHashSet();

        private static readonly IImmutableSet<string> DisposeMethods = new []
        {
            "Dispose",
            "Close"
        }.ToImmutableHashSet();

        private static readonly IImmutableSet<string> FactoryMethods = new []
        {
            "System.IO.File.Create",
            "System.IO.File.Open",
            "System.Drawing.Image.FromFile",
            "System.Drawing.Image.FromStream"
        }.ToImmutableHashSet();

        private class NodeAndSymbol
        {
            public SyntaxNode Node { get; set; }
            public ISymbol Symbol { get; set; }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(
                analysisContext =>
                {
                    var trackedNodesAndSymbols = new HashSet<NodeAndSymbol>();
                    var excludedSymbols = new HashSet<ISymbol>();

                    TrackInitializedLocalsAndPrivateFields(analysisContext, trackedNodesAndSymbols);
                    TrackAssignmentsToLocalsAndPrivateFields(analysisContext, trackedNodesAndSymbols);

                    ExcludeDisposedAndClosedLocalsAndPrivateFields(analysisContext, excludedSymbols);
                    ExcludeReturnedPassedAndAliasedLocalsAndPrivateFields(analysisContext, excludedSymbols);

                    analysisContext.RegisterCompilationEndAction(
                        c =>
                        {
                            foreach (var trackedNodeAndSymbol in trackedNodesAndSymbols)
                            {
                                if (!excludedSymbols.Contains(trackedNodeAndSymbol.Symbol))
                                {
                                    c.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule, trackedNodeAndSymbol.Node.GetLocation(), trackedNodeAndSymbol.Symbol.Name), c.Compilation);
                                }
                            }
                        });
                });
        }

        private static void TrackInitializedLocalsAndPrivateFields(CompilationStartAnalysisContext context, ISet<NodeAndSymbol> trackedNodesAndSymbols)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    VariableDeclarationSyntax declaration;
                    if (c.Node.IsKind(SyntaxKind.LocalDeclarationStatement))
                    {
                        declaration = ((LocalDeclarationStatementSyntax)c.Node).Declaration;
                    }
                    else if (c.Node.IsKind(SyntaxKind.FieldDeclaration))
                    {
                        var fieldDeclaration = (FieldDeclarationSyntax)c.Node;
                        if (!fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
                        {
                            return;
                        }

                        declaration = fieldDeclaration.Declaration;
                    }
                    else
                    {
                        throw new ArgumentException();
                    }

                    foreach (var variableNode in declaration.Variables.Where(v => v.Initializer != null && IsInstantiation(v.Initializer.Value, c.SemanticModel)))
                    {
                        trackedNodesAndSymbols.Add(new NodeAndSymbol { Node = variableNode, Symbol = c.SemanticModel.GetDeclaredSymbol(variableNode) });
                    }
                },
                SyntaxKind.LocalDeclarationStatement,
                SyntaxKind.FieldDeclaration);
        }

        private static void TrackAssignmentsToLocalsAndPrivateFields(CompilationStartAnalysisContext context, ISet<NodeAndSymbol> trackedNodesAndSymbols)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var assignment = c.Node as AssignmentExpressionSyntax;
                    if (assignment.Parent.IsKind(SyntaxKind.UsingStatement))
                    {
                        return;
                    }

                    var referencedSymbol = c.SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
                    if (referencedSymbol == null || !IsLocalOrPrivateField(referencedSymbol))
                    {
                        return;
                    }

                    if (IsInstantiation(assignment.Right, c.SemanticModel))
                    {
                        trackedNodesAndSymbols.Add(new NodeAndSymbol { Node = assignment, Symbol = referencedSymbol });
                    }
                },
                SyntaxKind.SimpleAssignmentExpression);
        }

        private static bool IsLocalOrPrivateField(ISymbol symbol)
        {
            return symbol.Kind == SymbolKind.Local ||
                (symbol.Kind == SymbolKind.Field && symbol.DeclaredAccessibility == Accessibility.Private);
        }

        private static void ExcludeDisposedAndClosedLocalsAndPrivateFields(CompilationStartAnalysisContext context, ISet<ISymbol> excludedSymbols)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    SimpleNameSyntax name;
                    ExpressionSyntax expression;

                    if (c.Node.IsKind(SyntaxKind.InvocationExpression))
                    {
                        var invocation = (InvocationExpressionSyntax)c.Node;
                        var memberAccessNode = invocation.Expression as MemberAccessExpressionSyntax;

                        name = memberAccessNode?.Name;
                        expression = memberAccessNode?.Expression;
                    }
                    else if (c.Node.IsKind(SyntaxKind.ConditionalAccessExpression))
                    {
                        var conditionalAccess = (ConditionalAccessExpressionSyntax)c.Node;
                        var invocation = conditionalAccess.WhenNotNull as InvocationExpressionSyntax;
                        if (invocation == null)
                        {
                            return;
                        }

                        var memberBindingNode = invocation.Expression as MemberBindingExpressionSyntax;

                        name = memberBindingNode?.Name;
                        expression = conditionalAccess.Expression;
                    }
                    else
                    {
                        throw new ArgumentException();
                    }

                    if (name == null || !DisposeMethods.Contains(name.Identifier.Text))
                    {
                        return;
                    }

                    var referencedSymbol = c.SemanticModel.GetSymbolInfo(expression).Symbol;
                    if (referencedSymbol != null && IsLocalOrPrivateField(referencedSymbol))
                    {
                        excludedSymbols.Add(referencedSymbol);
                    }
                },
                SyntaxKind.InvocationExpression,
                SyntaxKind.ConditionalAccessExpression);
        }

        private static void ExcludeReturnedPassedAndAliasedLocalsAndPrivateFields(CompilationStartAnalysisContext context, ISet<ISymbol> excludedSymbols)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    ExpressionSyntax expression;
                    if (c.Node.IsKind(SyntaxKind.IdentifierName))
                    {
                        expression = (IdentifierNameSyntax)c.Node;
                    }
                    else if (c.Node.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    {

                        var memberAccess = (MemberAccessExpressionSyntax)c.Node;
                        if (!memberAccess.Expression.IsKind(SyntaxKind.ThisExpression))
                        {
                            return;
                        }
                        expression = memberAccess;
                    }
                    else
                    {
                        throw new ArgumentException();
                    }

                    if (IsStandaloneExpression(expression))
                    {
                        var referencedSymbol = c.SemanticModel.GetSymbolInfo(c.Node).Symbol;
                        if (referencedSymbol != null && IsLocalOrPrivateField(referencedSymbol))
                        {
                            excludedSymbols.Add(referencedSymbol);
                        }
                    }
                },
                SyntaxKind.IdentifierName,
                SyntaxKind.SimpleMemberAccessExpression);
        }

        private static bool IsStandaloneExpression(ExpressionSyntax expression)
        {
            var parentAsAssignment = expression.Parent as AssignmentExpressionSyntax;

            return !(expression.Parent is ExpressionSyntax) ||
                (parentAsAssignment != null && object.ReferenceEquals(expression, parentAsAssignment.Right));
        }

        private static bool IsInstantiation(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            return IsNewTrackedTypeObjectCreation(expression, semanticModel) ||
                IsFactoryMethodInvocation(expression, semanticModel);
        }

        private static bool IsNewTrackedTypeObjectCreation(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            if (!expression.IsKind(SyntaxKind.ObjectCreationExpression))
            {
                return false;
            }

            ITypeSymbol type = semanticModel.GetTypeInfo(expression).Type;
            if (type == null || !TrackedTypes.Contains(type.ToDisplayString()))
            {
                return false;
            }

            var constructor = semanticModel.GetSymbolInfo(expression).Symbol as IMethodSymbol;
            return constructor != null && !constructor.Parameters.Any(p => DisposableMemberInNonDisposableClass.ImplementsIDisposable(p.Type));
        }

        private static bool IsFactoryMethodInvocation(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var n = expression as InvocationExpressionSyntax;
            if (n == null)
            {
                return false;
            }

            var methodSymbol = semanticModel.GetSymbolInfo(n).Symbol as IMethodSymbol;
            if (methodSymbol == null)
            {
                return false;
            }

            var methodQualifiedName = methodSymbol.ContainingType.ToDisplayString() + "." + methodSymbol.Name;
            return FactoryMethods.Contains(methodQualifiedName);
        }
    }
}
