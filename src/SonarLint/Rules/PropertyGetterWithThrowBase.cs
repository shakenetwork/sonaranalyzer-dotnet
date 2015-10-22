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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;

namespace SonarLint.Rules.Common
{
    public abstract class PropertyGetterWithThrowBase : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2372";
        internal const string Title = "Exceptions should not be thrown from property getters";
        internal const string Description =
            "Property getters should be simple operations that are always safe to call. If exceptions need to be thrown, it is best to convert the property to a method.";
        internal const string MessageFormat = "Remove the exception throwing from this property getter, or refactor the property into a method.";
        internal const string Category = "SonarLint";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }
    }

    public abstract class PropertyGetterWithThrowBase<TLanguageKindEnum, TAccessorSyntax> : PropertyGetterWithThrowBase
        where TLanguageKindEnum : struct
        where TAccessorSyntax : SyntaxNode
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockStartActionInNonGenerated<TLanguageKindEnum>(
                cbc =>
                {
                    var propertyGetter = cbc.CodeBlock as TAccessorSyntax;
                    if (propertyGetter == null ||
                        !IsGetter(propertyGetter) ||
                        IsIndexer(propertyGetter))
                    {
                        return;
                    }

                    cbc.RegisterSyntaxNodeAction(
                        c =>
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation()));
                        },
                        SyntaxKindsOfInterest.ToArray());
                });
        }

        protected abstract bool IsIndexer(TAccessorSyntax propertyGetter);

        protected abstract bool IsGetter(TAccessorSyntax propertyGetter);

        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
    }
}
