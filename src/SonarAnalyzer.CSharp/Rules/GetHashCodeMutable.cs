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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class GetHashCodeMutable : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2328";
        internal const string Title = "\"GetHashCode\" should not reference mutable fields";
        internal const string Description =
            "\"GetHashCode\" is used to file an object in a \"Dictionary\" or \"Hashtable\". " +
            "If \"GetHashCode\" uses non-\"readonly\" fields and those fields change after " +
            "the object is stored, the object immediately becomes mis-filed in the " +
            "\"Hashtable\". Any subsequent test to see if the object is in the \"Hashtable\" " +
            "will return a false negative.";
        internal const string MessageFormat = "Remove this use of \"{0}\" from the \"GetHashCode\" declaration, or make it \"readonly\".";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var methodSyntax = (MethodDeclarationSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(methodSyntax);

                    if (methodSymbol == null ||
                        methodSyntax.Identifier.Text != "GetHashCode" ||
                        !methodSyntax.Modifiers.Any(SyntaxKind.OverrideKeyword))
                    {
                        return;
                    }

                    ImmutableArray<ISymbol> baseMembers;
                    try
                    {
                        baseMembers = c.SemanticModel.LookupBaseMembers(methodSyntax.SpanStart);
                    }
                    catch (System.ArgumentException)
                    {
                        // this is expected on invalid code
                        return;
                    }

                    var fieldsOfClass = baseMembers
                        .Concat(methodSymbol.ContainingType.GetMembers())
                        .Select(symbol => symbol as IFieldSymbol)
                        .Where(symbol => symbol != null)
                        .ToImmutableHashSet();

                    var identifiers = methodSyntax.DescendantNodes()
                        .OfType<IdentifierNameSyntax>();

                    ReportOnFirstReferences(c, fieldsOfClass, identifiers);
                },
                SyntaxKind.MethodDeclaration);
        }

        private static void ReportOnFirstReferences(SyntaxNodeAnalysisContext context,
            ImmutableHashSet<IFieldSymbol> fieldsOfClass, IEnumerable<IdentifierNameSyntax> identifiers)
        {
            var syntaxNodes = new Dictionary<IFieldSymbol, List<IdentifierNameSyntax>>();

            foreach (var identifier in identifiers)
            {
                var identifierSymbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol as IFieldSymbol;
                if (identifierSymbol == null)
                {
                    continue;
                }

                if (!syntaxNodes.ContainsKey(identifierSymbol))
                {
                    if (!IsFieldRelevant(identifierSymbol, fieldsOfClass))
                    {
                        continue;
                    }

                    syntaxNodes.Add(identifierSymbol, new List<IdentifierNameSyntax>());
                }

                syntaxNodes[identifierSymbol].Add(identifier);
            }

            foreach (var identifierReferences in syntaxNodes.Values)
            {
                var firstPosition = identifierReferences.Select(id => id.SpanStart).Min();
                var identifier = identifierReferences.First(id => id.SpanStart == firstPosition);
                context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), identifier.Identifier.Text));
            }
        }

        private static bool IsFieldRelevant(IFieldSymbol fieldSymbol, ImmutableHashSet<IFieldSymbol> fieldsOfClass)
        {
            return fieldSymbol != null &&
                !fieldSymbol.IsConst &&
                !fieldSymbol.IsReadOnly &&
                fieldsOfClass.Contains(fieldSymbol);
        }
    }
}
