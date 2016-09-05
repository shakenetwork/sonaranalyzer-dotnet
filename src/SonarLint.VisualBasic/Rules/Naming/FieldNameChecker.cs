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

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarLint.Common;
using SonarLint.Helpers;
using System.Text.RegularExpressions;

namespace SonarLint.Rules.VisualBasic
{
    public abstract class FieldNameChecker : ParameterLoadingDiagnosticAnalyzer
    {
        private const string MaxTwoLongIdPattern = "([A-Z]{2})?";
        private const string PascalCasingInternalPattern = "([A-Z]{1,3}[a-z0-9]+)*";
        private const string CamelCasingInternalPattern = "[a-z][a-z0-9]*" + PascalCasingInternalPattern;
        internal const string PascalCasingPattern = "^" + PascalCasingInternalPattern + MaxTwoLongIdPattern + "$";
        internal const string CamelCasingPatternWithOptionalPrefixes = "^(s_|_)?" + CamelCasingInternalPattern + MaxTwoLongIdPattern + "$";

        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = false;

        public virtual string Pattern { get; set; }

        internal static bool IsRegexMatch(string name, string pattern)
        {
            return Regex.IsMatch(name, pattern, RegexOptions.CultureInvariant);
        }

        protected abstract bool IsCandidateSymbol(IFieldSymbol symbol);

        protected sealed override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var fieldDeclaration = (FieldDeclarationSyntax)c.Node;
                    foreach (var name in fieldDeclaration.Declarators.SelectMany(v => v.Names))
                    {
                        var symbol = c.SemanticModel.GetDeclaredSymbol(name) as IFieldSymbol;
                        if (symbol != null &&
                            IsCandidateSymbol(symbol) &&
                            !IsRegexMatch(symbol.Name, Pattern))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(SupportedDiagnostics.First(), name.GetLocation(), symbol.Name, Pattern));
                        }
                    }
                },
                SyntaxKind.FieldDeclaration);
        }
    }
}
