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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.CSharp.CodeAnalysis.Common;
using SonarQube.CSharp.CodeAnalysis.Common.Sqale;
using SonarQube.CSharp.CodeAnalysis.Helpers;

namespace SonarQube.CSharp.CodeAnalysis.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SynchronizationReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("bug", "multi-threading")]
    public class ThreadStaticWithInitializer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2996";
        internal const string Title = "\"ThreadStatic\" fields should not be initialized";
        internal const string Description =
            "When an object has a field annotated with \"ThreadStatic\", that field is shared within a given thread, " +
            "but unique across threads. Since a class' static initializer is only invoked for " +
            "the first thread created, it also means that only the first thread will have the expected initial values.";
        internal const string MessageFormat = "Remove this initialization of \"{0}\" or make it lazy.";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, 
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault, 
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        private const string ThreadStaticAttributeName = "System.ThreadStaticAttribute";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var fieldDeclaration = (FieldDeclarationSyntax)c.Node;
                    
                    if (fieldDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)) &&
                        HasThreadStaticAttribute(fieldDeclaration.AttributeLists, c.SemanticModel))
                    {
                        foreach (var variableDeclaratorSyntax in fieldDeclaration.Declaration.Variables
                            .Where(variableDeclaratorSyntax => variableDeclaratorSyntax.Initializer != null))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, variableDeclaratorSyntax.Identifier.GetLocation(),
                                variableDeclaratorSyntax.Identifier.ValueText));
                        }
                    }
                },
                SyntaxKind.FieldDeclaration);
        }
        private static bool HasThreadStaticAttribute(SyntaxList<AttributeListSyntax> attributeLists, SemanticModel semanticModel)
        {
            if (!attributeLists.Any())
            {
                return false;
            }

            return attributeLists
                .Any(attributeList => attributeList.Attributes
                    .Select(attribute => semanticModel.GetTypeInfo(attribute).Type)
                    .Any(attributeType => attributeType != null &&
                                          attributeType.ToDisplayString() == ThreadStaticAttributeName));
        }
    }
}
