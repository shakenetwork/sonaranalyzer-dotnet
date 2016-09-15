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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using System;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("15min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class WcfNonVoidOneWay : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3598";
        internal const string Title = "One-way \"OperationContract\" methods should have \"void\" return type";
        internal const string Description =
            "When declaring a Windows Communication Foundation (WCF) \"OperationContract\" method one-way, that service method won't return any result, not " +
            "even an underlying empty confirmation message. These are fire-and-forget methods that are useful in event-like communication. Specifying a return " +
            "type therefore does not make sense.";
        internal const string MessageFormat = "This method can't return any values because it is marked as one-way operation.";
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
                    var methodDeclaration = (MethodDeclarationSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(methodDeclaration);
                    if (methodSymbol == null ||
                        methodSymbol.ReturnsVoid)
                    {
                        return;
                    }

                    AttributeData attribute;
                    if (!TryGetOperationContract(methodSymbol, out attribute))
                    {
                        return;
                    }

                    var asyncPattern = attribute.NamedArguments.FirstOrDefault(na => na.Key == "AsyncPattern").Value.Value as bool?;
                    if (asyncPattern.HasValue &&
                        asyncPattern.Value)
                    {
                        return;
                    }

                    var isOneWay = attribute.NamedArguments.FirstOrDefault(na => na.Key == "IsOneWay").Value.Value as bool?;
                    if (isOneWay.HasValue &&
                        isOneWay.Value)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.ReturnType.GetLocation()));
                    }
                },
                SyntaxKind.MethodDeclaration);
        }

        private static bool TryGetOperationContract(IMethodSymbol methodSymbol, out AttributeData attribute)
        {
            attribute = methodSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass.Is(KnownType.System_ServiceModel_OperationContractAttribute));

            return attribute != null;
        }
    }
}
