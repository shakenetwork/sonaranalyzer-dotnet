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

using System.Linq;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Classification;
using SonarAnalyzer.Protobuf;
using System.Collections.Immutable;
using SonarAnalyzer.Helpers;

using static SonarAnalyzer.Protobuf.FileTokenInfo.Types;
using static SonarAnalyzer.Protobuf.FileTokenReferenceInfo.Types;

namespace SonarAnalyzer.Runner
{
    public class TokenCollector
    {
        private static readonly ISet<SymbolKind> DeclarationKinds = ImmutableHashSet.Create(
            SymbolKind.Event,
            SymbolKind.Field,
            SymbolKind.Local,
            SymbolKind.Method,
            SymbolKind.NamedType,
            SymbolKind.Parameter,
            SymbolKind.Property,
            SymbolKind.TypeParameter);

        private readonly SyntaxNode root;
        private readonly SemanticModel semanticModel;
        private readonly IEnumerable<ClassifiedSpan> classifiedSpans;

        private readonly string filePath;

        public TokenCollector(string filePath, Document document, Workspace workspace)
        {
            this.filePath = filePath;
            this.root = document.GetSyntaxRootAsync().Result;
            this.semanticModel = document.GetSemanticModelAsync().Result;
            this.classifiedSpans = Classifier.GetClassifiedSpans(semanticModel, root.FullSpan, workspace);
        }

        private class SymbolReferenceInfo
        {
            public SyntaxToken IdentifierToken { get; set; }
            public ISymbol Symbol { get; set; }
            public bool IsDeclaration { get; set; }
        }

        public FileTokenReferenceInfo FileTokenReferenceInfo
        {
            get
            {
                var allReferences = new List<SymbolReferenceInfo>();

                foreach (var classifiedSpan in classifiedSpans)
                {
                    var token = root.FindToken(classifiedSpan.TextSpan.Start, findInsideTrivia: true);
                    var reference = ProcessToken(token);
                    if (reference != null)
                    {
                        allReferences.Add(reference);
                    }
                }

                var tokenReferenceInfo = new FileTokenReferenceInfo
                {
                    FilePath = filePath
                };

                foreach (var allReference in allReferences.GroupBy(r => r.Symbol))
                {
                    var sr = GetSymbolReference(allReference);
                    if (sr != null)
                    {
                        tokenReferenceInfo.Reference.Add(sr);
                    }
                }

                return tokenReferenceInfo;
            }
        }

        public FileTokenInfo FileTokenInfo
        {
            get
            {
                var fileTokenInfo = new FileTokenInfo
                {
                    FilePath = filePath
                };

                foreach (var classifiedSpan in classifiedSpans)
                {
                    var tokenType = ClassificationTypeMapping.ContainsKey(classifiedSpan.ClassificationType)
                        ? ClassificationTypeMapping[classifiedSpan.ClassificationType]
                        : TokenType.Unknown;

                    var tokenInfo = new TokenInfo
                    {
                        TokenType = tokenType,
                        TextRange = GetTextRange(Location.Create(root.SyntaxTree, classifiedSpan.TextSpan).GetLineSpan())
                    };
                    fileTokenInfo.TokenInfo.Add(tokenInfo);
                }

                return fileTokenInfo;
            }
        }

        public CopyPasteTokenInfo FileTokenCpdInfo
        {
            get
            {
                var cpdTokenInfo = new CopyPasteTokenInfo
                {
                    FilePath = filePath
                };

                var tokens = this.root.DescendantTokens(n => !n.IsUsingDirective(), false);

                foreach (var token in tokens)
                {
                    var tokenInfo = new CopyPasteTokenInfo.Types.TokenInfo
                    {
                        TokenType = token.GetCpdValue(),
                        TextRange = GetTextRange(Location.Create(root.SyntaxTree, token.Span).GetLineSpan())
                    };

                    if (!string.IsNullOrWhiteSpace(tokenInfo.TokenType))
                    {
                        cpdTokenInfo.TokenInfo.Add(tokenInfo);
                    }
                }

                return cpdTokenInfo;
            }
        }

        private SymbolReference GetSymbolReference(IEnumerable<SymbolReferenceInfo> allReference)
        {
            var declaration = allReference.FirstOrDefault(r => r.IsDeclaration);
            if (declaration == null)
            {
                return null;
            }

            var sr = new SymbolReference
            {
                Declaration = GetTextRange(Location.Create(root.SyntaxTree, declaration.IdentifierToken.Span).GetLineSpan())
            };

            var references = allReference.Where(r => !r.IsDeclaration).Select(r => r.IdentifierToken);
            foreach (var reference in references)
            {
                sr.Reference.Add(GetTextRange(Location.Create(root.SyntaxTree, reference.Span).GetLineSpan()));
            }

            return sr;
        }

        private static TextRange GetTextRange(FileLinePositionSpan lineSpan)
        {
            return new TextRange
            {
                StartLine = lineSpan.StartLinePosition.GetLineNumberToReport(),
                EndLine = lineSpan.EndLinePosition.GetLineNumberToReport(),
                StartOffset = lineSpan.StartLinePosition.Character,
                EndOffset = lineSpan.EndLinePosition.Character
            };
        }

        private SymbolReferenceInfo ProcessToken(SyntaxToken token)
        {
            if (!token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.IdentifierToken) &&
                !token.IsKind(Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.IdentifierToken))
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
                    return new SymbolReferenceInfo
                    {
                        IdentifierToken = token,
                        Symbol = declaredSymbol,
                        IsDeclaration = true
                    };
                }
            }
            else
            {
                var node = token.GetBindableParent();
                if (node != null)
                {
                    var symbol = semanticModel.GetSymbolInfo(node).Symbol;
                    if (symbol == null)
                    {
                        return null;
                    }

                    if (symbol.DeclaringSyntaxReferences.Any())
                    {
                        return new SymbolReferenceInfo
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
                        return new SymbolReferenceInfo
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

        private static readonly IDictionary<string, TokenType> ClassificationTypeMapping = new Dictionary<string, TokenType>
        {
            { ClassificationTypeNames.ClassName, TokenType.TypeName },
            { ClassificationTypeNames.DelegateName, TokenType.TypeName },
            { ClassificationTypeNames.EnumName, TokenType.TypeName },
            { ClassificationTypeNames.InterfaceName, TokenType.TypeName },
            { ClassificationTypeNames.ModuleName, TokenType.TypeName },
            { ClassificationTypeNames.StructName, TokenType.TypeName },

            { ClassificationTypeNames.TypeParameterName, TokenType.TypeName },

            { ClassificationTypeNames.Comment, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentAttributeName, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentAttributeQuotes, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentAttributeValue, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentCDataSection, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentComment, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentDelimiter, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentEntityReference, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentName, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentProcessingInstruction, TokenType.Comment },
            { ClassificationTypeNames.XmlDocCommentText, TokenType.Comment },

            { ClassificationTypeNames.NumericLiteral, TokenType.NumericLiteral },

            { ClassificationTypeNames.StringLiteral, TokenType.StringLiteral },
            { ClassificationTypeNames.VerbatimStringLiteral, TokenType.StringLiteral },

            { ClassificationTypeNames.Keyword, TokenType.Keyword },
            { ClassificationTypeNames.PreprocessorKeyword, TokenType.Keyword }
        }.ToImmutableDictionary();
    }
}
