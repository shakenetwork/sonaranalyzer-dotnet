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
using System.Collections.Generic;
using System.Linq;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class DisposeNotImplementingDispose : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2953";
        internal const string Title = "Methods named \"Dispose\" should implement \"IDisposable.Dispose\"";
        internal const string Description =
            "\"Dispose\" as a method name should be used exclusively to implement \"IDisposable.Dispose\" to prevent any " +
            "confusion. It may be tempting to create a \"Dispose\" method for other purposes, but doing so will result in " +
            "confusion and likely lead to problems in production.";
        internal const string MessageFormat = "Either implement \"IDisposable.Dispose\", or totally rename this method to prevent confusion.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const string DisposeMethodName = "Dispose";

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterCompilationStartAction(
                analysisContext =>
                {
                    var disposeMethod = GetDisposeMethod(analysisContext.Compilation);
                    if (disposeMethod == null)
                    {
                        return;
                    }

                    var disposeMethodsCalledFromDispose = MultiValueDictionary<INamedTypeSymbol, IMethodSymbol>.Create<HashSet<IMethodSymbol>>();
                    var implementingDisposeMethods = ImmutableHashSet<IMethodSymbol>.Empty;
                    var allDisposeMethods = ImmutableHashSet<IMethodSymbol>.Empty;

                    analysisContext.RegisterSymbolAction(c =>
                        CollectDisposeMethods(c, disposeMethod, ref allDisposeMethods, ref implementingDisposeMethods),
                        SymbolKind.Method);

                    analysisContext.RegisterCodeBlockStartAction<SyntaxKind>(
                        cbc =>
                        {
                            var methodDeclaration = cbc.CodeBlock as MethodDeclarationSyntax;
                            if (methodDeclaration == null ||
                                methodDeclaration.Identifier.ValueText != DisposeMethodName)
                            {
                                return;
                            }

                            var declaredMethodSymbol = cbc.SemanticModel.GetDeclaredSymbol(methodDeclaration);
                            if (declaredMethodSymbol == null ||
                                !MethodIsDisposeImplementation(declaredMethodSymbol, disposeMethod))
                            {
                                return;
                            }

                            var disposableType = declaredMethodSymbol.ContainingType;
                            cbc.RegisterSyntaxNodeAction(
                                c => CollectDisposeMethodsCalledFromDispose((InvocationExpressionSyntax) c.Node,
                                    c.SemanticModel, disposableType, disposeMethodsCalledFromDispose),
                                SyntaxKind.InvocationExpression);
                        });

                    analysisContext.RegisterCompilationEndAction(
                        c =>
                            ReportDisposeMethods(allDisposeMethods, implementingDisposeMethods,
                                disposeMethodsCalledFromDispose, c));
                });
        }

        private static void CollectDisposeMethodsCalledFromDispose(InvocationExpressionSyntax invocationExpression,
            SemanticModel semanticModel, INamedTypeSymbol disposableType,
            MultiValueDictionary<INamedTypeSymbol, IMethodSymbol> disposeMethodsCalledFromDispose)
        {
            var invokedMethod = semanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;
            if (invokedMethod == null ||
                invokedMethod.Name != DisposeMethodName ||
                !disposableType.Equals(invokedMethod.ContainingType))
            {
                return;
            }

            disposeMethodsCalledFromDispose.AddWithKey(disposableType, invokedMethod);
        }

        private static void ReportDisposeMethods(ImmutableHashSet<IMethodSymbol> allDisposeMethods, ImmutableHashSet<IMethodSymbol> implementingDisposeMethods,
            MultiValueDictionary<INamedTypeSymbol, IMethodSymbol> disposeMethodsCalledFromDispose, CompilationAnalysisContext context)
        {
            foreach (var dispose in allDisposeMethods.Except(implementingDisposeMethods))
            {
                if (MethodCalledFromDispose(disposeMethodsCalledFromDispose, dispose))
                {
                    continue;
                }

                foreach (var declaringSyntaxReference in dispose.DeclaringSyntaxReferences)
                {
                    var methodDeclaration =
                        declaringSyntaxReference.GetSyntax() as MethodDeclarationSyntax;
                    if (methodDeclaration != null)
                    {
                        context.ReportDiagnosticIfNonGenerated(
                            Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation()),
                            context.Compilation);
                    }
                }
            }
        }

        private static void CollectDisposeMethods(SymbolAnalysisContext context, IMethodSymbol disposeMethod,
            ref ImmutableHashSet<IMethodSymbol> allDisposeMethods,
            ref ImmutableHashSet<IMethodSymbol> implementingDisposeMethods)
        {

            var methodSymbol = context.Symbol as IMethodSymbol;
            if (methodSymbol == null ||
                methodSymbol.Name != DisposeMethodName)
            {
                return;
            }

            allDisposeMethods = allDisposeMethods.Add(methodSymbol);

            if (methodSymbol.IsOverride ||
                MethodIsDisposeImplementation(methodSymbol, disposeMethod) ||
                MethodMightImplementDispose(methodSymbol))
            {
                implementingDisposeMethods = implementingDisposeMethods.Add(methodSymbol);
            }
        }

        private static bool MethodCalledFromDispose(MultiValueDictionary<INamedTypeSymbol, IMethodSymbol> disposeMethodsCalledFromDispose, IMethodSymbol dispose)
        {
            return disposeMethodsCalledFromDispose.ContainsKey(dispose.ContainingType) &&
                   disposeMethodsCalledFromDispose[dispose.ContainingType].Contains(dispose);
        }

        private static bool MethodIsDisposeImplementation(IMethodSymbol methodSymbol, IMethodSymbol disposeMethod)
        {
            return methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(disposeMethod));
        }

        private static bool MethodMightImplementDispose(IMethodSymbol declaredMethodSymbol)
        {
            var containingType = declaredMethodSymbol.ContainingType;

            if (containingType.BaseType != null && containingType.BaseType.Kind == SymbolKind.ErrorType)
            {
                return true;
            }

            var interfaces = containingType.AllInterfaces;
            foreach (var @interface in interfaces)
            {
                if (@interface.Kind == SymbolKind.ErrorType)
                {
                    return true;
                }

                var interfaceMethods = @interface.GetMembers().OfType<IMethodSymbol>();
                foreach (var interfaceMethod in interfaceMethods)
                {
                    if (declaredMethodSymbol.Equals(containingType.FindImplementationForInterfaceMember(interfaceMethod)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static IMethodSymbol GetDisposeMethod(Compilation compilation)
        {
            return (IMethodSymbol)compilation.GetSpecialType(SpecialType.System_IDisposable)
                .GetMembers("Dispose")
                .SingleOrDefault();
        }
    }
}
