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

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.UsabilityAccessibility)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious)]
    public class WcfMissingContractAttribute : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3597";
        internal const string Title = "\"ServiceContract\" and \"OperationContract\" attributes should be used together";
        internal const string Description =
            "The \"ServiceContract\" attribute specifies that a class or interface defines the communication contract of a Windows Communication Foundation " +
            "(WCF) service. The service operations of this class or interface are defined by \"OperationContract\" attributes added to methods. It doesn't " +
            "make sense to define a contract without any service operations; thus, in a \"ServiceContract\" class or interface at least one method should be " +
            "annotated with \"OperationContract\". Similarly, WCF only serves \"OperationContract\" methods that are defined inside \"ServiceContract\" " +
            "classes or interfaces; thus, this rule also checks that \"ServiceContract\" is added to the containing type of \"OperationContract\" methods.";
        internal const string MessageFormat = "Add the \"{0}\" attribute to {1}.";
        internal const string MessageOperation = "the methods of this {0}";
        internal const string MessageService = " this {0}";
        internal const string Category = SonarAnalyzer.Common.Category.Design;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var namedType = (INamedTypeSymbol)c.Symbol;
                    if (namedType.Is(TypeKind.Struct))
                    {
                        return;
                    }

                    var hasServiceContract = HasServiceContract(namedType);
                    var hasAnyMethodWithOperationContract = HasAnyMethodWithoperationContract(namedType);

                    if (!(hasServiceContract ^ hasAnyMethodWithOperationContract))
                    {
                        return;
                    }

                    var declarationSyntax = GetTypeDeclaration(namedType, c.Compilation);
                    if (declarationSyntax == null)
                    {
                        return;
                    }

                    string message;
                    string attributeToAdd;

                    if (hasServiceContract)
                    {
                        message = MessageOperation;
                        attributeToAdd = "OperationContract";
                    }
                    else
                    {
                        message = MessageService;
                        attributeToAdd = "ServiceContract";
                    }

                    var classOrInterface = namedType.IsClass() ? "class" : "interface";
                    message = string.Format(message, classOrInterface);

                    c.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule,
                        declarationSyntax.Identifier.GetLocation(), attributeToAdd, message));
                },
                SymbolKind.NamedType);
        }

        private static bool HasAnyMethodWithoperationContract(INamedTypeSymbol namedType)
        {
            return namedType.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.GetAttributes()
                    .Any(a => a.AttributeClass.Is(KnownType.System_ServiceModel_OperationContractAttribute)));
        }

        private static bool HasServiceContract(INamedTypeSymbol namedType)
        {
            return namedType.GetAttributes()
                .Any(a => a.AttributeClass.Is(KnownType.System_ServiceModel_ServiceContractAttribute));
        }

        private static TypeDeclarationSyntax GetTypeDeclaration(INamedTypeSymbol namedType, Compilation compilation)
        {
            return namedType.DeclaringSyntaxReferences
                .Where(sr => !sr.SyntaxTree.IsGenerated(compilation))
                .Select(sr => sr.GetSyntax() as TypeDeclarationSyntax)
                .FirstOrDefault(s => s != null);
        }
    }
}
