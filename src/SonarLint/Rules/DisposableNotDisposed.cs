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

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("bug", "cwe", "denial-of-service", "security")]
    public class DisposableNotDisposed : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2930";
        internal const string Title = "\"IDisposables\" should be disposed";
        internal const string Description =
            "You can't rely on garbage collection to clean up everything. Specifically, you can't " +
            "count on it to release non-memory resources such as \"File\"s. For that, there's the " +
            "\"IDisposable\" interface, and the contract that \"Dispose\" will always be called on " +
            "such objects. When an \"IDisposable\" is a class member, then it's up to that class " +
            "to call \"Dispose\" on it, ideally in its own \"Dispose\" method. If it's a local variable, " +
            "then it should be instantiated with a \"using\" clause to prompt automatic cleanup when " +
            "it goes out of scope.";
        internal const string MessageFormat = "\"Dispose\" of \"{0}\".";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockStartActionInNonGenerated<SyntaxKind>(analysisContext =>
            {
                var localsAssigned = ImmutableHashSet<ILocalSymbol>.Empty;
                var localsAssignedInUsing = ImmutableHashSet<ILocalSymbol>.Empty;
                var localsDisposed = ImmutableHashSet<ILocalSymbol>.Empty;
                var localsReturned = ImmutableHashSet<ILocalSymbol>.Empty;
                var disposeMethod = GetDisposeMethod(analysisContext.SemanticModel.Compilation);

                if (disposeMethod == null)
                {
                    return;
                }

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectAssignedLocalDisposables((AssignmentExpressionSyntax)c.Node, c.SemanticModel,
                        ref localsAssigned),
                    SyntaxKind.SimpleAssignmentExpression);

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectInitializedLocalDisposables((VariableDeclaratorSyntax)c.Node, c.SemanticModel,
                        ref localsAssigned),
                    SyntaxKind.VariableDeclarator);

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectDisposableLocalFromUsing((UsingStatementSyntax)c.Node, c.SemanticModel,
                        ref localsAssignedInUsing),
                    SyntaxKind.UsingStatement);

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectDisposedSymbol((InvocationExpressionSyntax)c.Node, c.SemanticModel, disposeMethod,
                        IsDisposableLocalSymbol, ref localsDisposed),
                    SyntaxKind.InvocationExpression);

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectDisposedSymbol((ConditionalAccessExpressionSyntax)c.Node, c.SemanticModel, disposeMethod,
                    IsDisposableLocalSymbol, ref localsDisposed),
                    SyntaxKind.ConditionalAccessExpression);

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectReturnedLocals((ReturnStatementSyntax)c.Node, c.SemanticModel,
                        ref localsReturned),
                    SyntaxKind.ReturnStatement);

                analysisContext.RegisterCodeBlockEndAction(c =>
                {
                    var internallyInitializedLocals = localsAssigned.Except(localsAssignedInUsing);
                    var nonDisposedLocals = internallyInitializedLocals
                        .Except(localsDisposed)
                        .Except(localsReturned);

                    ReportIssues(nonDisposedLocals, c.ReportDiagnostic);
                });
            });

            context.RegisterCompilationStartAction(analysisContext =>
            {
                var disposableFields = ImmutableHashSet<IFieldSymbol>.Empty;
                var fieldsAssigned = ImmutableHashSet<IFieldSymbol>.Empty;
                var fieldsDisposed = ImmutableHashSet<IFieldSymbol>.Empty;

                var disposeMethod = GetDisposeMethod(analysisContext.Compilation);
                if (disposeMethod == null)
                {
                    return;
                }

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectDisposableFields((FieldDeclarationSyntax)c.Node, c.SemanticModel,
                        ref disposableFields, ref fieldsAssigned),
                    SyntaxKind.FieldDeclaration);

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectAssignedDisposableFields((AssignmentExpressionSyntax)c.Node, c.SemanticModel,
                        ref fieldsAssigned),
                    SyntaxKind.SimpleAssignmentExpression);

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectDisposedSymbol((InvocationExpressionSyntax)c.Node, c.SemanticModel, disposeMethod,
                    DisposableMemberInNonDisposableClass.IsNonStaticNonPublicDisposableField, ref fieldsDisposed),
                    SyntaxKind.InvocationExpression);

                analysisContext.RegisterSyntaxNodeAction(
                    c => CollectDisposedSymbol((ConditionalAccessExpressionSyntax)c.Node, c.SemanticModel, disposeMethod,
                    DisposableMemberInNonDisposableClass.IsNonStaticNonPublicDisposableField, ref fieldsDisposed),
                    SyntaxKind.ConditionalAccessExpression);

                analysisContext.RegisterCompilationEndAction(c =>
                {
                    var internallyInitializedFields = disposableFields.Intersect(fieldsAssigned);
                    var nonDisposedFields = internallyInitializedFields.Except(fieldsDisposed);

                    ReportIssues(nonDisposedFields, diagnostic => { c.ReportDiagnosticIfNonGenerated(diagnostic); });
                });
            });
        }

        internal static IMethodSymbol GetDisposeMethod(Compilation compilation)
        {
            return (IMethodSymbol)compilation.GetSpecialType(SpecialType.System_IDisposable)
                .GetMembers("Dispose")
                .SingleOrDefault();
        }

        private static void CollectDisposedSymbol<T>(InvocationExpressionSyntax invocation, SemanticModel semanticModel,
            IMethodSymbol disposeMethod, Predicate<T> isSymbolRelevant, ref ImmutableHashSet<T> fieldsDisposed)
             where T : class, ISymbol
        {
            T fieldSymbol;
            if (TryGetDisposedSymbol(invocation, semanticModel, disposeMethod,
                isSymbolRelevant, out fieldSymbol))
            {
                fieldsDisposed = fieldsDisposed.Add(fieldSymbol);
            }
        }
        private static void CollectDisposedSymbol<T>(ConditionalAccessExpressionSyntax conditionalAccess, SemanticModel semanticModel,
            IMethodSymbol disposeMethod, Predicate<T> isSymbolRelevant, ref ImmutableHashSet<T> fieldsDisposed)
             where T : class, ISymbol
        {
            T fieldSymbol;
            if (TryGetDisposedSymbol(conditionalAccess, semanticModel, disposeMethod,
                isSymbolRelevant, out fieldSymbol))
            {
                fieldsDisposed = fieldsDisposed.Add(fieldSymbol);
            }
        }

        private static void CollectAssignedDisposableFields(AssignmentExpressionSyntax assignment, SemanticModel semanticModel,
            ref ImmutableHashSet<IFieldSymbol> fieldsAssigned)
        {
            IFieldSymbol fieldSymbol;
            if (TryGetLocallyConstructedSymbol(assignment, semanticModel,
                DisposableMemberInNonDisposableClass.IsNonStaticNonPublicDisposableField, out fieldSymbol))
            {
                fieldsAssigned = fieldsAssigned.Add(fieldSymbol);
            }
        }

        private static void CollectDisposableFields(FieldDeclarationSyntax field, SemanticModel semanticModel,
            ref ImmutableHashSet<IFieldSymbol> disposableFields, ref ImmutableHashSet<IFieldSymbol> fieldsAssigned)
        {
            foreach (var variableDeclaratorSyntax in field.Declaration.Variables)
            {
                var fieldSymbol = semanticModel.GetDeclaredSymbol(variableDeclaratorSyntax) as IFieldSymbol;

                if (!DisposableMemberInNonDisposableClass.IsNonStaticNonPublicDisposableField(fieldSymbol))
                {
                    continue;
                }

                disposableFields = disposableFields.Add(fieldSymbol);

                if (variableDeclaratorSyntax.Initializer == null ||
                    !(variableDeclaratorSyntax.Initializer.Value is ObjectCreationExpressionSyntax))
                {
                    return;
                }

                fieldsAssigned = fieldsAssigned.Add(fieldSymbol);
            }
        }

        private static void CollectReturnedLocals(ReturnStatementSyntax returnStatement, SemanticModel semanticModel,
            ref ImmutableHashSet<ILocalSymbol> localsReturned)
        {
            if (returnStatement.Expression == null)
            {
                return;
            }

            var localSymbol = semanticModel.GetSymbolInfo(returnStatement.Expression).Symbol as ILocalSymbol;
            if (IsDisposableLocalSymbol(localSymbol))
            {
                localsReturned = localsReturned.Add(localSymbol);
            }
        }

        private static void CollectDisposableLocalFromUsing(UsingStatementSyntax usingStatement, SemanticModel semanticModel,
            ref ImmutableHashSet<ILocalSymbol> localsAssignedInUsing)
        {
            if (usingStatement.Declaration == null)
            {
                return;
            }

            foreach (var declarator in usingStatement.Declaration.Variables)
            {
                ILocalSymbol localSymbol;
                if (TryGetLocallyConstructedInitializedLocalSymbol(declarator, semanticModel, out localSymbol))
                {
                    localsAssignedInUsing = localsAssignedInUsing.Add(localSymbol);
                }
            }
        }

        private static void CollectInitializedLocalDisposables(VariableDeclaratorSyntax variableDeclarator, SemanticModel semanticModel, ref ImmutableHashSet<ILocalSymbol> localsAssigned)
        {
            ILocalSymbol localSymbol;
            if (TryGetLocallyConstructedInitializedLocalSymbol(variableDeclarator, semanticModel, out localSymbol))
            {
                localsAssigned = localsAssigned.Add(localSymbol);
            }
        }

        private static void CollectAssignedLocalDisposables(AssignmentExpressionSyntax assignment, SemanticModel semanticModel, ref ImmutableHashSet<ILocalSymbol> localsAssigned)
        {
            ILocalSymbol localSymbol;
            if (TryGetLocallyConstructedSymbol(assignment, semanticModel, IsDisposableLocalSymbol, out localSymbol, true))
            {
                localsAssigned = localsAssigned.Add(localSymbol);
            }
        }

        private static void ReportIssues<T>(ImmutableHashSet<T> nonDisposedSymbols, Action<Diagnostic> report)
            where T : class, ISymbol
        {
            foreach (var nonDisposedSymbol in nonDisposedSymbols)
            {
                var declarationReference = nonDisposedSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (declarationReference == null)
                {
                    continue;
                }
                var declarator = declarationReference.GetSyntax() as VariableDeclaratorSyntax;
                if (declarator == null)
                {
                    continue;
                }

                report(Diagnostic.Create(Rule, declarator.Identifier.GetLocation(), declarator.Identifier.ValueText));
            }
        }

        private static bool TryGetDisposedSymbol<T>(InvocationExpressionSyntax invocation, SemanticModel semanticModel,
            IMethodSymbol disposeMethod, Predicate<T> isSymbolRelevant, out T localSymbol) where T : class, ISymbol
        {
            localSymbol = null;
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
            {
                return false;
            }

            localSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol as T;
            if (!isSymbolRelevant(localSymbol))
            {
                return false;
            }

            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            return methodSymbol != null &&
                   (methodSymbol.Equals(disposeMethod) ||
                   methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(disposeMethod)));
        }

        private static bool TryGetDisposedSymbol<T>(ConditionalAccessExpressionSyntax conditionalAccess, SemanticModel semanticModel,
            IMethodSymbol disposeMethod, Predicate<T> isSymbolRelevant, out T localSymbol) where T : class, ISymbol
        {
            localSymbol = null;

            localSymbol = semanticModel.GetSymbolInfo(conditionalAccess.Expression).Symbol as T;
            if (!isSymbolRelevant(localSymbol))
            {
                return false;
            }

            var methodSymbol = semanticModel.GetSymbolInfo(conditionalAccess.WhenNotNull).Symbol as IMethodSymbol;
            return methodSymbol != null &&
                   (methodSymbol.Equals(disposeMethod) ||
                   methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(disposeMethod)));
        }

        private static bool TryGetLocallyConstructedSymbol<T>(AssignmentExpressionSyntax assignment, SemanticModel semanticModel,
            Predicate<T> isSymbolRelevant, out T localSymbol, bool doCheckNonLocalDisposables = false) where T : class, ISymbol
        {
            localSymbol = null;
            var objectCreation = assignment.Right as ObjectCreationExpressionSyntax;
            if (objectCreation == null ||
                (doCheckNonLocalDisposables && MightUseNonLocalDisposable(objectCreation, semanticModel)))
            {
                return false;
            }

            localSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol as T;
            return isSymbolRelevant(localSymbol);
        }

        private static bool TryGetLocallyConstructedInitializedLocalSymbol(VariableDeclaratorSyntax declarator, SemanticModel semanticModel,
            out ILocalSymbol localSymbol)
        {
            localSymbol = null;
            if (declarator.Initializer == null)
            {
                return false;
            }

            var objectCreation = declarator.Initializer.Value as ObjectCreationExpressionSyntax;
            if (objectCreation == null || MightUseNonLocalDisposable(objectCreation, semanticModel))
            {
                return false;
            }

            localSymbol = semanticModel.GetDeclaredSymbol(declarator) as ILocalSymbol;
            return IsDisposableLocalSymbol(localSymbol);
        }

        private static bool MightUseNonLocalDisposable(ObjectCreationExpressionSyntax objectCreation,
            SemanticModel semanticModel)
        {
            return MightUseNonLocalDisposableInArguments(objectCreation, semanticModel) ||
                   MightUseNonLocalDisposableInInitializer(objectCreation, semanticModel);
        }

        private static bool MightUseNonLocalDisposableInInitializer(ObjectCreationExpressionSyntax objectCreation,
            SemanticModel semanticModel)
        {
            if (objectCreation.Initializer == null)
            {
                return false;
            }

            return objectCreation.Initializer.Expressions
                .Select(
                    expression =>
                    {
                        var assignment = expression as AssignmentExpressionSyntax;
                        return assignment == null ? expression : assignment.Right;
                    })
                .Any(expression => HasDisposableNotLocalIdentifier(expression, semanticModel));
        }

        private static bool MightUseNonLocalDisposableInArguments(ObjectCreationExpressionSyntax objectCreation,
            SemanticModel semanticModel)
        {
            if (objectCreation.ArgumentList == null)
            {
                return false;
            }

            return objectCreation.ArgumentList.Arguments
                .Any(argument => HasDisposableNotLocalIdentifier(argument.Expression, semanticModel));
        }

        private static bool HasDisposableNotLocalIdentifier(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            return expression
                .DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .Any(identifier => IsDisposableNotLocalIdentifier(identifier, semanticModel));
        }

        private static bool IsDisposableNotLocalIdentifier(IdentifierNameSyntax identifier, SemanticModel semanticModel)
        {
            var expressionSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (expressionSymbol == null)
            {
                return true;
            }

            var localSymbol = expressionSymbol as ILocalSymbol;
            var expressionType = semanticModel.GetTypeInfo(identifier).Type;

            return localSymbol == null &&
                   expressionType != null &&
                   DisposableMemberInNonDisposableClass.ImplementsIDisposable(expressionType as INamedTypeSymbol);
        }

        private static bool IsDisposableLocalSymbol(ILocalSymbol localSymbol)
        {
            return localSymbol != null &&
                   DisposableMemberInNonDisposableClass.ImplementsIDisposable(localSymbol.Type as INamedTypeSymbol);
        }
    }
}
