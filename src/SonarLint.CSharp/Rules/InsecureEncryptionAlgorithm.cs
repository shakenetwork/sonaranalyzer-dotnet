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
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SecurityFeatures)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cwe, Tag.OwaspA6, Tag.Security)]
    public class InsecureEncryptionAlgorithm : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2278";
        internal const string Title = "Neither DES (Data Encryption Standard) nor DESede (3DES) should be used";
        internal const string Description =
            "According to the US National Institute of Standards and Technology (NIST), the Data Encryption Standard (DES) is " +
            "no longer considered secure";
        internal const string MessageFormat = "Use the recommended AES (Advanced Encryption Standard) instead.";
        internal const string Category = SonarLint.Common.Category.Security;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const string BaseEncryptionAlgorithmCreate = "System.Security.Cryptography.SymmetricAlgorithm.Create";

        private static readonly string[] AlgorithmNames =
        {
            "DES",
            "3DES",
            "TripleDES"
        };

        private static readonly string[] MethodNamesToReachEncryptionAlgorithm =
        {
            "System.Security.Cryptography.DES.Create",
            "System.Security.Cryptography.TripleDES.Create"
        };

        private static readonly string[] BaseClassNamesForEncryptionAlgorithm =
        {
            "System.Security.Cryptography.DES",
            "System.Security.Cryptography.TripleDES"
        };

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckObjectCreation(c),
                SyntaxKind.ObjectCreationExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckInvocation(c),
                SyntaxKind.InvocationExpression);
        }

        private static void CheckInvocation(SyntaxNodeAnalysisContext c)
        {
            var invocation = (InvocationExpressionSyntax)c.Node;

            var methodSymbol = c.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol;
            if (methodSymbol == null ||
                methodSymbol.ContainingType == null)
            {
                return;
            }

            var methodName = $"{methodSymbol.ContainingType}.{methodSymbol.Name}";
            if (MethodNamesToReachEncryptionAlgorithm.Contains(methodName) ||
                IsBaseEncryptionCreateCalled(methodName, invocation.ArgumentList))
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            }
        }

        private static void CheckObjectCreation(SyntaxNodeAnalysisContext c)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)c.Node;

            var typeInfo = c.SemanticModel.GetTypeInfo(objectCreation);
            if (typeInfo.ConvertedType == null || typeInfo.ConvertedType is IErrorTypeSymbol)
            {
                return;
            }

            var insecureArgorithmType = GetInsecureAlgorithmBase(typeInfo.ConvertedType);

            if (insecureArgorithmType != null &&
                BaseClassNamesForEncryptionAlgorithm.Contains(insecureArgorithmType.ToString()))
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.Type.GetLocation()));
            }
        }

        private static bool IsBaseEncryptionCreateCalled(string methodName, ArgumentListSyntax argumentList)
        {
            if (methodName != BaseEncryptionAlgorithmCreate)
            {
                return false;
            }

            if (argumentList.Arguments.Count == 0 ||
                !argumentList.Arguments.First().Expression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return false;
            }

            var algorithmNameCandidate = ((LiteralExpressionSyntax)argumentList.Arguments.First().Expression).Token.ValueText;
            var algorithmName = AlgorithmNames
                .FirstOrDefault(alg =>
                    algorithmNameCandidate.StartsWith(alg, System.StringComparison.Ordinal));

            return algorithmName != null;
        }

        private static ITypeSymbol GetInsecureAlgorithmBase(ITypeSymbol type)
        {
            var currentType = type;

            while (currentType != null &&
                !BaseClassNamesForEncryptionAlgorithm.Contains(currentType.ToString()))
            {
                currentType = currentType.BaseType;
            }

            return currentType;
        }
    }
}
