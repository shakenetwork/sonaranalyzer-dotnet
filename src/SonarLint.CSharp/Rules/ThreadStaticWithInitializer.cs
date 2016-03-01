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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("20min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SynchronizationReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.MultiThreading)]
    public class ThreadStaticWithInitializer : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2996";
        internal const string Title = "\"ThreadStatic\" fields should not be initialized";
        internal const string Description =
            "When an object has a field annotated with \"ThreadStatic\", that field is shared within a given thread, " +
            "but unique across threads. Since a class' static initializer is only invoked for " +
            "the first thread created, it also means that only the first thread will have the expected initial values.";
        internal const string MessageFormat = "Remove this initialization of \"{0}\" or make it lazy.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());
        
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var fieldDeclaration = (FieldDeclarationSyntax)c.Node;

                    if (fieldDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)) &&
                        HasThreadStaticAttribute(fieldDeclaration.AttributeLists, c.SemanticModel))
                    {
                        foreach (var variableDeclaratorSyntax in fieldDeclaration.Declaration.Variables
                            .Where(variableDeclaratorSyntax => variableDeclaratorSyntax.Initializer != null))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, variableDeclaratorSyntax.Initializer.GetLocation(),
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

            return attributeLists.Any(attributeList => 
                attributeList.Attributes.Any(attribute => semanticModel.GetTypeInfo(attribute).Type.Is(KnownType.System_ThreadStaticAttribute)));
        }
    }
}
