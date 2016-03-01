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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;
using System.Linq;

namespace SonarLint.Rules.Common
{
    public abstract class StringConcatenationInLoopBase : SonarDiagnosticAnalyzer, IMultiLanguageDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S1643";
        protected const string Title = "Strings should not be concatenated using \"+\" in a loop";
        protected const string Description =
            "\"StringBuilder\" is more efficient than string concatenation, especially when the operator is repeated over and over as in loops.";
        protected const string MessageFormat = "Use a StringBuilder instead.";
        protected const string Category = SonarLint.Common.Category.Performance;
        protected const Severity RuleSeverity = Severity.Major;
        protected const bool IsActivatedByDefault = true;

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }
        GeneratedCodeRecognizer IMultiLanguageDiagnosticAnalyzer.GeneratedCodeRecognizer => GeneratedCodeRecognizer;
    }

    public abstract class StringConcatenationInLoopBase<TLanguageKindEnum, TAssignmentExpression, TBinaryExpression>
            : StringConcatenationInLoopBase
        where TLanguageKindEnum : struct
        where TAssignmentExpression : SyntaxNode
        where TBinaryExpression : SyntaxNode
    {
        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c => CheckCompoundAssignment(c),
                CompoundAssignmentKinds.ToArray());

            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c => CheckSimpleAssignment(c),
                SimpleAssignmentKinds.ToArray());
        }

        private void CheckSimpleAssignment(SyntaxNodeAnalysisContext context)
        {
            var assignment = (TAssignmentExpression)context.Node;
            if (!IsString(GetLeft(assignment), context.SemanticModel))
            {
                return;
            }

            var addExpression = GetRight(assignment) as TBinaryExpression;
            if (addExpression == null ||
                !ExpressionIsConcatenation(addExpression) ||
                !AreEquivalent(GetLeft(assignment), GetLeft(addExpression)))
            {
                return;
            }

            SyntaxNode nearestLoop;
            if (!TryGetNearestLoop(assignment, out nearestLoop))
            {
                return;
            }

            if (IsDefinedInLoop(GetLeft(assignment), nearestLoop, context.SemanticModel))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation()));
        }

        private void CheckCompoundAssignment(SyntaxNodeAnalysisContext context)
        {
            var addAssignment = (TAssignmentExpression)context.Node;
            if (!IsString(GetLeft(addAssignment), context.SemanticModel))
            {
                return;
            }

            SyntaxNode nearestLoop;
            if (!TryGetNearestLoop(addAssignment, out nearestLoop))
            {
                return;
            }

            var symbol = context.SemanticModel.GetSymbolInfo(GetLeft(addAssignment)).Symbol as ILocalSymbol;
            if (symbol != null &&
                IsDefinedInLoop(GetLeft(addAssignment), nearestLoop, context.SemanticModel))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, addAssignment.GetLocation()));
        }

        protected abstract bool ExpressionIsConcatenation(TBinaryExpression addExpression);
        protected abstract SyntaxNode GetLeft(TAssignmentExpression assignment);
        protected abstract SyntaxNode GetRight(TAssignmentExpression assignment);
        protected abstract SyntaxNode GetLeft(TBinaryExpression binary);
        protected abstract bool AreEquivalent(SyntaxNode node1, SyntaxNode node2);

        protected abstract ImmutableArray<TLanguageKindEnum> SimpleAssignmentKinds { get; }
        protected abstract ImmutableArray<TLanguageKindEnum> CompoundAssignmentKinds { get; }

        private static bool IsString(SyntaxNode node, SemanticModel semanticModel)
        {
            return semanticModel.GetTypeInfo(node).Type
                .Is(KnownType.System_String);
        }

        private bool TryGetNearestLoop(SyntaxNode node, out SyntaxNode nearestLoop)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                if (IsInLoop(parent))
                {
                    nearestLoop = parent;
                    return true;
                }
                parent = parent.Parent;
            }
            nearestLoop = null;
            return false;
        }

        protected abstract bool IsInLoop(SyntaxNode node);

        private bool IsDefinedInLoop(SyntaxNode expression, SyntaxNode nearestLoopForConcatenation,
                SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetSymbolInfo(expression).Symbol as ILocalSymbol;
            if (symbol == null)
            {
                return false;
            }

            var declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (declaration == null)
            {
                return false;
            }

            SyntaxNode nearestLoop;
            if (!TryGetNearestLoop(declaration, out nearestLoop))
            {
                return false;
            }

            return nearestLoop == nearestLoopForConcatenation;
        }
    }
}
