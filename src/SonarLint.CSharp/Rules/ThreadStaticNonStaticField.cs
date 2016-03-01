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
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug, Tag.Unused)]
    public class ThreadStaticNonStaticField : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3005";
        internal const string Title = "\"ThreadStatic\" should not be used on non-static fields";
        internal const string Description =
            "When a non-static class field is annotated with \"ThreadStatic\", the code seems to show that the " +
            "field can have different values for different calling threads, but that's not the case, since the " +
            "\"ThreadStatic\" attribute is simply ignored on non-static fields.";
        internal const string MessageFormat = "Remove the \"ThreadStatic\" attribute from this definition.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
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
                    var fieldDeclaration = (FieldDeclarationSyntax) c.Node;
                    AttributeSyntax threadStaticAttribute;
                    if (!fieldDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)) &&
                        TryGetThreadStaticAttribute(fieldDeclaration.AttributeLists, c.SemanticModel, out threadStaticAttribute))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, threadStaticAttribute.Name.GetLocation()));
                    }
                },
                SyntaxKind.FieldDeclaration);
        }

        private static bool TryGetThreadStaticAttribute(SyntaxList<AttributeListSyntax> attributeLists, SemanticModel semanticModel, out AttributeSyntax threadStaticAttribute)
        {
            threadStaticAttribute = null;

            if (!attributeLists.Any())
            {
                return false;
            }

            foreach (var attributeList in attributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var attributeType = semanticModel.GetTypeInfo(attribute).Type;

                    if (attributeType.Is(KnownType.System_ThreadStaticAttribute))
                    {
                        threadStaticAttribute = attribute;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
