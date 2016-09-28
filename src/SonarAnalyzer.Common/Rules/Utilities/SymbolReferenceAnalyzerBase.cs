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
using Microsoft.CodeAnalysis;
using SonarAnalyzer.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarAnalyzer.Rules
{
    public abstract class SymbolReferenceAnalyzerBase : UtilityAnalyzerBase<SymbolReferenceInfo>
    {
        protected const string DiagnosticId = "S9999-symbolRef";
        protected const string Title = "Symbol reference calculator";

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, string.Empty, string.Empty, DiagnosticSeverity.Warning,
                true, customTags: WellKnownDiagnosticTags.NotConfigurable);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        internal const string SymbolReferenceFileName = "symbol-reference.pb";

        protected sealed override string FileName => SymbolReferenceFileName;

        protected sealed override SymbolReferenceInfo GetMessage(SyntaxTree syntaxTree, SemanticModel semanticModel)
        {
            return CalculateSymbolReferenceInfo(syntaxTree, semanticModel, IsIdentifier, GetBindableParent);
        }

        internal static SymbolReferenceInfo CalculateSymbolReferenceInfo(SyntaxTree syntaxTree, SemanticModel semanticModel,
            Func<SyntaxToken, bool> isIdentifier, Func<SyntaxToken, SyntaxNode> getBindableParent)
        {
            var allReferences = new List<SymRefInfo>();

            var tokens = syntaxTree.GetRoot().DescendantTokens();
            foreach (var token in tokens)
            {
                var reference = GetSymRefInfo(token, semanticModel, isIdentifier, getBindableParent);
                if (reference != null)
                {
                    allReferences.Add(reference);
                }
            }

            var symbolReferenceInfo = new SymbolReferenceInfo
            {
                FilePath = syntaxTree.FilePath
            };

            foreach (var allReference in allReferences.GroupBy(r => r.Symbol))
            {
                var sr = GetSymbolReference(allReference, syntaxTree);
                if (sr != null)
                {
                    symbolReferenceInfo.Reference.Add(sr);
                }
            }

            return symbolReferenceInfo;
        }

        private static SymbolReferenceInfo.Types.SymbolReference GetSymbolReference(IEnumerable<SymRefInfo> allReference, SyntaxTree tree)
        {
            var declaration = allReference.FirstOrDefault(r => r.IsDeclaration);
            if (declaration == null)
            {
                return null;
            }

            var sr = new SymbolReferenceInfo.Types.SymbolReference
            {
                Declaration = GetTextRange(Location.Create(tree, declaration.IdentifierToken.Span).GetLineSpan())
            };

            var references = allReference.Where(r => !r.IsDeclaration).Select(r => r.IdentifierToken);
            foreach (var reference in references)
            {
                sr.Reference.Add(GetTextRange(Location.Create(tree, reference.Span).GetLineSpan()));
            }

            return sr;
        }

        private static readonly ISet<SymbolKind> DeclarationKinds = ImmutableHashSet.Create(
            SymbolKind.Event,
            SymbolKind.Field,
            SymbolKind.Local,
            SymbolKind.Method,
            SymbolKind.NamedType,
            SymbolKind.Parameter,
            SymbolKind.Property,
            SymbolKind.TypeParameter);

        private static SymRefInfo GetSymRefInfo(SyntaxToken token, SemanticModel semanticModel,
            Func<SyntaxToken, bool> isIdentifier, Func<SyntaxToken, SyntaxNode> getBindableParent)
        {
            if (!isIdentifier(token))
            {
                // For the time being, we only handle identifer tokens.
                // We could also handle keywords, such as this, base
                return null;
            }

            var declaredSymbol = semanticModel.GetDeclaredSymbol(token.Parent);
            if (declaredSymbol != null)
            {
                if (DeclarationKinds.Contains(declaredSymbol.Kind))
                {
                    return new SymRefInfo
                    {
                        IdentifierToken = token,
                        Symbol = declaredSymbol,
                        IsDeclaration = true
                    };
                }
            }
            else
            {
                var node = getBindableParent(token);
                if (node != null)
                {
                    var symbol = semanticModel.GetSymbolInfo(node).Symbol;
                    if (symbol == null)
                    {
                        return null;
                    }

                    if (symbol.DeclaringSyntaxReferences.Any())
                    {
                        return new SymRefInfo
                        {
                            IdentifierToken = token,
                            Symbol = symbol,
                            IsDeclaration = false
                        };
                    }

                    var ctorSymbol = symbol as IMethodSymbol;
                    if (ctorSymbol != null &&
                        ctorSymbol.MethodKind == MethodKind.Constructor &&
                        ctorSymbol.IsImplicitlyDeclared)
                    {
                        return new SymRefInfo
                        {
                            IdentifierToken = token,
                            Symbol = ctorSymbol.ContainingType,
                            IsDeclaration = false
                        };
                    }
                }
            }
            return null;
        }

        internal /* for MsBuild12 support */ abstract SyntaxNode GetBindableParent(SyntaxToken token);

        protected abstract bool IsIdentifier(SyntaxToken token);

        public class SymRefInfo
        {
            public SyntaxToken IdentifierToken { get; set; }
            public ISymbol Symbol { get; set; }
            public bool IsDeclaration { get; set; }
        }
    }
}
