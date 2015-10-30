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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;
using System.Collections.Generic;

namespace SonarLint.Rules.Common
{
    public abstract class PublicConstantFieldBase : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2339";
        internal const string Title = "Public constant members should not be used";
        internal const string Description =
            "Constant members are copied at compile time to the call sites, instead of being fetched at runtime. " +
            "This means that you should use constants to hold values that by definition will never change, such as " +
            "\"Zero\". In practice, those cases are uncommon, and therefore it is generally better to avoid constant " +
            "members.";
        internal const string MessageFormat = "Change this constant to a {0} property.";
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }
    }

    public abstract class PublicConstantFieldBase<TLanguageKindEnum, TFieldDeclarationSyntax, TFieldName> : PublicConstantFieldBase
        where TLanguageKindEnum : struct
        where TFieldDeclarationSyntax: SyntaxNode
        where TFieldName: SyntaxNode
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var field = (TFieldDeclarationSyntax)c.Node;
                    var variables = GetVariables(field);

                    if (!variables.Any())
                    {
                        return;
                    }

                    var anyVariable = variables.First();
                    var symbol = c.SemanticModel.GetDeclaredSymbol(anyVariable) as IFieldSymbol;
                    if (symbol == null ||
                        !symbol.IsConst ||
                        !PublicMethodWithMultidimensionalArrayBase.IsPublic(symbol))
                    {
                        return;
                    }

                    foreach (var variable in variables)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, GetReportLocation(variable), MessageArgument));
                    }
                },
                FieldDeclarationKind);
        }

        protected abstract IEnumerable<TFieldName> GetVariables(TFieldDeclarationSyntax node);
        public abstract TLanguageKindEnum FieldDeclarationKind { get; }
        public abstract string MessageArgument { get; }

        protected abstract Location GetReportLocation(TFieldName node);
    }
}
