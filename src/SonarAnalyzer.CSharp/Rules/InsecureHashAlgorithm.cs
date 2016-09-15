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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("30min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.SecurityFeatures)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cwe, Tag.OwaspA6, Tag.SansTop25Porous, Tag.Security)]
    public class InsecureHashAlgorithm : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2070";
        internal const string Title = "SHA-1 and Message-Digest hash algorithms should not be used";
        internal const string Description =
            "The MD5 algorithm and its successor, SHA-1, are no longer considered secure, because " +
            "it is too easy to create hash collisions with them.That is, it takes too little " +
            "computational effort to come up with a different input that produces the same MD5 or " +
            "SHA-1 hash, and using the new, same-hash value gives an attacker the same access as " +
            "if he had the originally-hashed value.This applies as well to the other Message-Digest" +
            " algorithms: MD2, MD4, MD6.";
        internal const string MessageFormat = "Use a stronger encryption algorithm than {0}.";
        internal const string Category = SonarAnalyzer.Common.Category.Security;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        private static readonly Dictionary<KnownType, string> InsecureHashAlgorithmTypeNames = new Dictionary<KnownType, string>
        {
            { KnownType.System_Security_Cryptography_SHA1, "SHA1"},
            { KnownType.System_Security_Cryptography_MD5, "MD5"}
        };

        private static readonly string[] MethodNamesToReachHashAlgorithm =
        {
            "System.Security.Cryptography.CryptoConfig.CreateFromName",
            "System.Security.Cryptography.HashAlgorithm.Create"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckObjectCreation(c),
                SyntaxKind.ObjectCreationExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckInvocation(c),
                SyntaxKind.InvocationExpression);
        }

        private static void CheckInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol;
            if (methodSymbol?.ContainingType == null)
            {
                return;
            }

            var methodName = $"{methodSymbol.ContainingType}.{methodSymbol.Name}";
            string algorithmName;
            if (MethodNamesToReachHashAlgorithm.Contains(methodName) &&
                TryGetAlgorithmName(invocation.ArgumentList, out algorithmName))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), algorithmName));
            }
        }

        private static void CheckObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

            var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);

            if (typeInfo.ConvertedType == null || typeInfo.ConvertedType is IErrorTypeSymbol)
            {
                return;
            }

            ITypeSymbol insecureArgorithmType;
            if (!TryGetInsecureAlgorithmBase(typeInfo.ConvertedType, out insecureArgorithmType))
            {
                return;
            }

            var insecureHashAlgorithmType = InsecureHashAlgorithmTypeNames.FirstOrDefault(t => insecureArgorithmType.Is(t.Key));
            if (!insecureHashAlgorithmType.Equals(default(KeyValuePair<KnownType, string>)))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.Type.GetLocation(), insecureHashAlgorithmType.Value));
            }
        }

        private static bool TryGetAlgorithmName(ArgumentListSyntax argumentList, out string algorithmName)
        {
            if (argumentList == null ||
                argumentList.Arguments.Count == 0 ||
                !argumentList.Arguments.First().Expression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                algorithmName = null;
                return false;
            }

            var algorithmNameCandidate = ((LiteralExpressionSyntax)argumentList.Arguments.First().Expression).Token.ValueText;
            algorithmName = InsecureHashAlgorithmTypeNames.Values
                .FirstOrDefault(alg =>
                    algorithmNameCandidate.StartsWith(alg, System.StringComparison.Ordinal));

            return algorithmName != null;
        }

        private static bool TryGetInsecureAlgorithmBase(ITypeSymbol type, out ITypeSymbol insecureAlgorithmBase)
        {
            insecureAlgorithmBase = null;
            var currentType = type;

            while (currentType != null &&
                !currentType.Is(KnownType.System_Security_Cryptography_HashAlgorithm))
            {
                insecureAlgorithmBase = currentType;
                currentType = currentType.BaseType;
            }

            return insecureAlgorithmBase != null;
        }
    }
}
