/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Helpers;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Description, IsActivatedByDefault)]
    [Tags("bug")]
    public class GetHashCodeMutable : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2328";
        internal const string Description = "\"GetHashCode\" should not reference mutable fields";
        internal const string MessageFormat = "Remove this use of \"{0}\".";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: "http://nemo.sonarqube.org/coding_rules#rule_key=csharpsquid%3AS2328");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var methodSyntax = (MethodDeclarationSyntax) c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(methodSyntax);

                    if (methodSymbol == null ||
                        methodSyntax.Identifier.Text != "GetHashCode" ||
                        !methodSyntax.Modifiers.Any(SyntaxKind.OverrideKeyword))
                    {
                        return;
                    }
                    
                    var fieldsOfClass = c.SemanticModel.LookupBaseMembers(methodSyntax.SpanStart)
                        .Concat(methodSymbol.ContainingType.GetMembers())
                        .Select(symbol => symbol as IFieldSymbol)
                        .Where(symbol => symbol != null)
                        .ToList();

                    var identifiers = methodSyntax.DescendantNodes()
                        .OfType<IdentifierNameSyntax>();

                    foreach (var identifier in identifiers)
                    {
                        var identifierSymbol = c.SemanticModel.GetSymbolInfo(identifier).Symbol as IFieldSymbol;

                        if (identifierSymbol == null ||
                            identifierSymbol.IsConst ||
                            identifierSymbol.IsReadOnly ||
                            !fieldsOfClass.Contains(identifierSymbol))
                        {
                            continue;
                        }
                        
                        c.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), identifier.Identifier.Text));
                    }
                },
                SyntaxKind.MethodDeclaration);
        }
    }
}
