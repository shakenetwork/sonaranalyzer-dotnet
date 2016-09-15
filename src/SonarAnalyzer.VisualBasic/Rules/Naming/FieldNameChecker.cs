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

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using System.Text.RegularExpressions;

namespace SonarAnalyzer.Rules.VisualBasic
{
    public abstract class FieldNameChecker : ParameterLoadingDiagnosticAnalyzer
    {
        private const string MaxTwoLongIdPattern = "([A-Z]{2})?";
        internal const string PascalCasingInternalPattern = "([A-Z]{1,3}[a-z0-9]+)*" + MaxTwoLongIdPattern;
        internal const string CamelCasingInternalPattern = "[a-z][a-z0-9]*" + PascalCasingInternalPattern;
        internal const string PascalCasingPattern = "^" + PascalCasingInternalPattern + "$";
        internal const string CamelCasingPattern = "^" + CamelCasingInternalPattern + "$";
        internal const string CamelCasingPatternWithOptionalPrefixes = "^(s_|_)?" + CamelCasingInternalPattern + "$";

        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
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
                    foreach (var name in fieldDeclaration.Declarators.SelectMany(v => v.Names).Where(n => n != null))
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
