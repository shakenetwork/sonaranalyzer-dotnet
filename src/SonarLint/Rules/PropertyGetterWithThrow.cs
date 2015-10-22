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
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Rules.Common;
using SonarLint.Helpers;
using System;

namespace SonarLint.Rules
{
    namespace CSharp
    {
        using Microsoft.CodeAnalysis.CSharp;
        using Microsoft.CodeAnalysis.CSharp.Syntax;

        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        [SqaleConstantRemediation("20min")]
        [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
        [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
        [Tags(Tag.ErrorHandling)]
        public class PropertyGetterWithThrow : PropertyGetterWithThrowBase<SyntaxKind, AccessorDeclarationSyntax>
        {
            private static readonly ImmutableArray<SyntaxKind> kindsOfInterest = ImmutableArray.Create(SyntaxKind.ThrowStatement);
            public override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => kindsOfInterest;

            protected override bool IsGetter(AccessorDeclarationSyntax propertyGetter) => propertyGetter.IsKind(SyntaxKind.GetAccessorDeclaration);
            protected override bool IsIndexer(AccessorDeclarationSyntax propertyGetter) => propertyGetter.Parent.Parent is IndexerDeclarationSyntax;
        }
    }

    namespace VisualBasic
    {
        using Microsoft.CodeAnalysis.VisualBasic;
        using Microsoft.CodeAnalysis.VisualBasic.Syntax;

        [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
        [SqaleConstantRemediation("20min")]
        [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
        [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
        [Tags(Tag.ErrorHandling)]
        public class PropertyGetterWithThrow : PropertyGetterWithThrowBase<SyntaxKind, AccessorBlockSyntax>
        {
            private static readonly ImmutableArray<SyntaxKind> kindsOfInterest = ImmutableArray.Create(SyntaxKind.ThrowStatement);
            public override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => kindsOfInterest;

            protected override bool IsGetter(AccessorBlockSyntax propertyGetter) => propertyGetter.IsKind(SyntaxKind.GetAccessorBlock);

            protected override bool IsIndexer(AccessorBlockSyntax propertyGetter)
            {
                var propertyBlock = propertyGetter.Parent as PropertyBlockSyntax;
                if (propertyBlock == null)
                {
                    return false;
                }
                return propertyBlock.PropertyStatement.ParameterList != null &&
                    propertyBlock.PropertyStatement.ParameterList.Parameters.Any();
            }
        }
    }
}