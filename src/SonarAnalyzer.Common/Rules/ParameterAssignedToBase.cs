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
using System.Linq;
using Microsoft.CodeAnalysis;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules
{
    public abstract class ParameterAssignedToBase : SonarDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S1226";
        protected const string Title = "Method parameters and caught exceptions should not be reassigned";
        protected const string Description =
            "While it is technically correct to assign to parameters from within method bodies, it is better to " +
            "use temporary variables to store intermediate results. This rule will typically detect cases where a " +
            "constructor parameter is assigned to itself instead of a field of the same name, i.e. when \"this\"/\"Me\" was " +
            "forgotten. Allowing parameters to be assigned to also reduces the code readability as developers will " +
            "not be able to know whether the original parameter or some temporary variable is being accessed without " +
            "going through the whole method. Moreover, some developers might also expect assignments of method " +
            "parameters to be visible from callers, which is not the case and can confuse them. All parameters " +
            "should be treated as read-only.";
        protected const string MessageFormat = "Introduce a new variable instead of reusing the parameter \"{0}\".";
        protected const string Category = SonarAnalyzer.Common.Category.Maintainability;
        protected const Severity RuleSeverity = Severity.Major;
        protected const bool IsActivatedByDefault = false;

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }
    }

    public abstract class ParameterAssignedToBase<TLanguageKindEnum, TAssignmentStatementSyntax> : ParameterAssignedToBase
        where TLanguageKindEnum : struct
        where TAssignmentStatementSyntax : SyntaxNode
    {
        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var assignment = (TAssignmentStatementSyntax)c.Node;
                    var left = GetAssignedNode(assignment);
                    var symbol = c.SemanticModel.GetSymbolInfo(left).Symbol;

                    if (symbol != null && (IsAssignmentToParameter(symbol) || IsAssignmentToCatchVariable(symbol, left)))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, left.GetLocation(), left.ToString()));
                    }
                },
                SyntaxKindsOfInterest.ToArray());
        }

        protected abstract bool IsAssignmentToCatchVariable(ISymbol symbol, SyntaxNode node);

        protected abstract bool IsAssignmentToParameter(ISymbol symbol);

        protected abstract SyntaxNode GetAssignedNode(TAssignmentStatementSyntax assignment);

        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
    }
}
