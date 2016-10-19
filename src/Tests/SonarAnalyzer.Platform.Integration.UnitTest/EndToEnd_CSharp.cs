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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Runner;
using System.IO;
using System.Linq;
using SonarAnalyzer.Protobuf;
using Google.Protobuf;
using System.Collections.Generic;
using System;
using FluentAssertions;

namespace SonarAnalyzer.Integration.UnitTest
{
    [TestClass]
    public class EndToEnd_CSharp
    {
        public TestContext TestContext { get; set; }

        internal const string OutputFolderName = "Output";
        internal const string TestResourcesFolderName = "TestResources";
        internal const string TestInputFileName = "TestInput";
        internal const string TestInputPath = TestResourcesFolderName + "\\" + TestInputFileName;

        private const string extension = ".cs";

        [TestInitialize]
        public void Initialize()
        {
            var tempInputFilePath = Path.Combine(TestContext.DeploymentDirectory, ParameterLoader.ParameterConfigurationFileName);
            File.Copy(Path.Combine(TestResourcesFolderName, "SonarLint.Cs.xml"), tempInputFilePath, true);

            Program.RunAnalysis(new ScannerAnalyzerConfiguration
            {
                InputConfigurationPath = tempInputFilePath,
                OutputFolderPath = OutputFolderName,
                Language = AnalyzerLanguage.CSharp.ToString(),
                WorkDirectoryConfigFilePath = Path.Combine(TestResourcesFolderName, "ProjectOutFolderPath.txt")
            });
        }

        internal class ExpectedTokenInfo
        {
            public int Index { get; set; }
            public string Text { get; set; }
            public TokenType Kind { get; set; }
        }

        internal class ExpectedReferenceInfo
        {
            public int Index { get; set; }
            public int NumberOfReferences { get; set; }
        }

        [TestMethod]
        public void Token_Types_Computed_CSharp()
        {
            var testFileContent = File.ReadAllLines(TestInputPath + extension);
            CheckTokenInfoFile(testFileContent, extension, 23, new[]
                {
                    new ExpectedTokenInfo { Index = 6, Kind = TokenType.Comment, Text = "// FIXME: fix this issue" },
                    new ExpectedTokenInfo { Index = 5, Kind = TokenType.TypeName, Text = "TTTestClass" },
                    new ExpectedTokenInfo { Index = 18, Kind = TokenType.TypeName, Text = "TTTestClass" },
                    new ExpectedTokenInfo { Index = 12, Kind = TokenType.Keyword, Text = "var" }
                });
        }

        [TestMethod]
        public void Cpd_Tokens_Computed_CSharp()
        {
            CheckCpdTokens(@"public class TTTestClass { public object MyMethod ( ) { using ( y = null ) { } var x = $num ; " +
                "if ( $num == $num ) { new TTTestClass ( ) ; return $str + x ; } return $char ; ; } }");
        }

        [TestMethod]
        public void Symbol_Reference_Computed_CSharp()
        {
            var testFileContent = File.ReadAllLines(TestInputPath + extension);
            CheckTokenReferenceFile(testFileContent, extension, 3, new[]
                {
                    new ExpectedReferenceInfo { Index = 0, NumberOfReferences = 1 },
                    new ExpectedReferenceInfo { Index = 1, NumberOfReferences = 0 },
                    new ExpectedReferenceInfo { Index = 2, NumberOfReferences = 1 }
                });
        }

        internal static void CheckCpdTokens(string tokenCpdExpected)
        {
            var cpdInfos = GetDeserializedData<CopyPasteTokenInfo>(Path.Combine(OutputFolderName, Rules.CopyPasteTokenAnalyzerBase.CopyPasteTokenFileName));

            Assert.AreEqual(1, cpdInfos.Count);
            var actual = string.Join(" ", cpdInfos[0].TokenInfo.Select(ti => ti.TokenValue));
            Assert.AreEqual(tokenCpdExpected, actual);
        }

        internal static void CheckTokenReferenceFile(string[] testFileContent, string extension,
            int totalReferenceCount, IEnumerable<ExpectedReferenceInfo> expectedReferences)
        {
            var refInfos = GetDeserializedData<SymbolReferenceInfo>(Path.Combine(OutputFolderName, Rules.SymbolReferenceAnalyzerBase.SymbolReferenceFileName));

            Assert.AreEqual(1, refInfos.Count);
            var refInfo = refInfos.First();
            Assert.AreEqual(TestInputPath + extension, refInfo.FilePath);
            Assert.AreEqual(totalReferenceCount, refInfo.Reference.Count);

            foreach (var expectedReference in expectedReferences)
            {
                var declarationPosition = refInfo.Reference[expectedReference.Index].Declaration;
                Assert.AreEqual(declarationPosition.StartLine, declarationPosition.EndLine);
                var tokenText = testFileContent[declarationPosition.StartLine - 1].Substring(
                    declarationPosition.StartOffset,
                    declarationPosition.EndOffset - declarationPosition.StartOffset);

                Assert.AreEqual(expectedReference.NumberOfReferences, refInfo.Reference[expectedReference.Index].Reference.Count);
                foreach (var reference in refInfo.Reference[expectedReference.Index].Reference)
                {
                    Assert.AreEqual(reference.StartLine, reference.EndLine);
                    var refText = testFileContent[reference.StartLine - 1].Substring(
                        reference.StartOffset,
                        reference.EndOffset - reference.StartOffset);
                    Assert.AreEqual(tokenText, refText);
                }
            }
        }

        internal static void CheckTokenInfoFile(string[] testInputFileLines, string extension, int totalTokenCount, IEnumerable<ExpectedTokenInfo> expectedTokens)
        {
            var tokenInfos = GetDeserializedData<TokenTypeInfo>(Path.Combine(OutputFolderName, Rules.TokenTypeAnalyzerBase.TokenTypeFileName));

            Assert.AreEqual(1, tokenInfos.Count);
            var token = tokenInfos.First();
            Assert.AreEqual(TestInputPath + extension, token.FilePath);
            Assert.AreEqual(totalTokenCount, token.TokenInfo.Count);

            foreach (var expectedToken in expectedTokens)
            {
                Assert.AreEqual(expectedToken.Kind, token.TokenInfo[expectedToken.Index].TokenType);

                var tokenPosition = token.TokenInfo[expectedToken.Index].TextRange;
                Assert.AreEqual(tokenPosition.StartLine, tokenPosition.EndLine);
                var tokenText = testInputFileLines[tokenPosition.StartLine - 1].Substring(
                    tokenPosition.StartOffset,
                    tokenPosition.EndOffset - tokenPosition.StartOffset);
                Assert.AreEqual(expectedToken.Text, tokenText);
            }
        }

        [TestMethod]
        public void Metrics_Are_Present()
        {
            var metrics = GetDeserializedData<MetricsInfo>(Path.Combine(OutputFolderName, Rules.MetricsAnalyzerBase.MetricsFileName));

            Assert.AreEqual(1, metrics.Count);
            var m = metrics.First();
            Assert.AreEqual($"{TestInputPath}{extension}", m.FilePath);

            Assert.AreEqual(1, m.ClassCount);
        }

        [TestMethod]
        public void Issues_Are_Present()
        {
            var fileIssues = GetDeserializedData<FileIssues>(Path.Combine(OutputFolderName, Program.IssuesFileName));
            Assert.AreEqual(1, fileIssues.Count);
            var issues = fileIssues.First().Issue;

            issues.Should().Contain(new FileIssues.Types.Issue
            {
                Id = "S1134",
                Message = "Take the required action to fix the issue indicated by this \"FIXME\" comment.",
                Location = new TextRange { StartLine = 8, EndLine = 8, StartOffset = 7, EndOffset = 12 }
            });

            issues.Should().Contain(new FileIssues.Types.Issue
            {
                Id = "S101",
                Message = "Rename class \"TTTestClass\" to match camel case naming rules, consider using \"TtTestClass\".",
                Location = new TextRange { StartLine = 6, EndLine = 6, StartOffset = 13, EndOffset = 24 }
            });

            issues.Should().Contain(new FileIssues.Types.Issue
            {
                Id = "S103",
                Message = "Split this 26 characters long line (which is greater than 10 authorized).",
                Location = new TextRange { StartLine = 20, EndLine = 20, StartOffset = 0, EndOffset = 26 }
            });
        }

        [TestMethod]
        public void No_UnExpected_Issues()
        {
            var fileIssues = GetDeserializedData<FileIssues>(Path.Combine(OutputFolderName, Program.IssuesFileName));
            Assert.IsFalse(fileIssues.First().Issue.Any(i => i.Id == "S1116"));
        }


        internal static List<TMessage> GetDeserializedData<TMessage>(string filePath)
            where TMessage : IMessage, new()
        {
            var messages = new List<TMessage>();

            using (var input = File.OpenRead(filePath))
            {
                while (input.Position != input.Length)
                {
                    var message = new TMessage();
                    message.MergeDelimitedFrom(input);
                    messages.Add(message);
                }
            }

            return messages;
        }
    }
}
