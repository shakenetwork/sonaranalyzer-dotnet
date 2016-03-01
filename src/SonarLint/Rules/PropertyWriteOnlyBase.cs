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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;

namespace SonarLint.Rules.Common
{
    public abstract class PropertyWriteOnlyBase : SonarDiagnosticAnalyzer, IMultiLanguageDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S2376";
        protected const string Title = "Write-only properties should not be used";
        protected const string Description =
            "Properties with only setters are confusing and counterintuitive. Instead, a property getter should be added if possible, " +
            "or the property should be replaced with a setter method.";
        protected const string MessageFormat = "Provide a getter for \"{0}\" or replace the property with a \"Set{0}\" method.";
        protected const string Category = SonarLint.Common.Category.Design;
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

    public abstract class PropertyWriteOnlyBase<TLanguageKindEnum, TPropertyDeclaration> : PropertyWriteOnlyBase
        where TLanguageKindEnum : struct
        where TPropertyDeclaration : SyntaxNode
    {
        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var prop = (TPropertyDeclaration)c.Node;
                    if (IsWriteOnlyProperty(prop))
                    {
                        var identifier = GetIdentifier(prop);

                        c.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(),
                            identifier.ValueText));
                    }

                },
                SyntaxKindsOfInterest.ToArray());
        }

        protected abstract SyntaxToken GetIdentifier(TPropertyDeclaration prop);
        protected abstract bool IsWriteOnlyProperty(TPropertyDeclaration prop);

        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
    }
}
