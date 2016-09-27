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

namespace SonarAnalyzer.Integration.UnitTest
{
    [TestClass]
    public class ProgramTest
    {
        public TestContext TestContext { get; set; }

        internal const string OutputFolderName = "Output";
        internal const string TestResourcesFolderName = "TestResources";
        internal const string TestInputFileName = "TestInput";
        internal const string TestInputPath = TestResourcesFolderName + "\\" + TestInputFileName;

        [TestMethod]
        public void End_To_End_CSharp()
        {
            var tempInputFilePath = Path.Combine(TestContext.DeploymentDirectory, ParameterLoader.ParameterConfigurationFileName);
            File.Copy(Path.Combine(TestResourcesFolderName, "SonarLint.Cs.xml"), tempInputFilePath, true);

            Program.Main(new[] {
                tempInputFilePath,
                OutputFolderName,
                AnalyzerLanguage.CSharp.ToString()});

            var textActual = new string(File.ReadAllText(Path.Combine(OutputFolderName, Program.AnalysisOutputFileName))
                .ToCharArray()
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray());

            CheckExpected(textActual);
            CheckNotExpected(textActual);

            CheckCollectedTokens(".cs",
                3, new[]
                {
                    new ExpectedReferenceInfo { Index = 0, NumberOfReferences = 2 },
                    new ExpectedReferenceInfo { Index = 1, NumberOfReferences = 0 },
                    new ExpectedReferenceInfo { Index = 2, NumberOfReferences = 1 }
                },
                78, new[]
                {
                    new ExpectedTokenInfo { Index = 8, Kind = TokenType.Comment, Text = "///" },
                    new ExpectedTokenInfo { Index = 31, Kind = TokenType.TypeName, Text = "TTTestClass" },
                    new ExpectedTokenInfo { Index = 62, Kind = TokenType.TypeName, Text = "TTTestClass" },
                    new ExpectedTokenInfo { Index = 49, Kind = TokenType.Keyword, Text = "var" }
                },
                @"public class TTTestClass { public object MyMethod ( ) { using ( y = null ) { } var x = $num ; " +
                "if ( $num == $num ) { new TTTestClass ( ) ; return $str + x ; } return $char ; ; } }");
        }

        [TestMethod]
        public void End_To_End_VisualBasic()
        {
            var tempInputFilePath = Path.Combine(TestContext.DeploymentDirectory, ParameterLoader.ParameterConfigurationFileName);
            File.Copy(Path.Combine(TestResourcesFolderName, "SonarLint.Vb.xml"), tempInputFilePath, true);

            Program.Main(new[] {
                tempInputFilePath,
                OutputFolderName,
                AnalyzerLanguage.VisualBasic.ToString()});

            CheckCollectedTokens(".vb",
                4, new[]
                {
                    new ExpectedReferenceInfo { Index = 0, NumberOfReferences = 2 },
                    new ExpectedReferenceInfo { Index = 1, NumberOfReferences = 0 },
                    new ExpectedReferenceInfo { Index = 2, NumberOfReferences = 1 },
                    new ExpectedReferenceInfo { Index = 3, NumberOfReferences = 0 }
                },
                67, new[]
                {
                    new ExpectedTokenInfo { Index = 8, Kind = TokenType.Comment, Text = "'''" },
                    new ExpectedTokenInfo { Index = 31, Kind = TokenType.TypeName, Text = "TTTestClass" },
                    new ExpectedTokenInfo { Index = 49, Kind = TokenType.TypeName, Text = "TTTestClass" },
                    new ExpectedTokenInfo { Index = 48, Kind = TokenType.Keyword, Text = "New" }
                },
                "Public Class TTTestClass Public Function MyMethod ( ) As Object Dim x = $num Dim y = New TTTestClass " +
                "If $num = $num Then Return x + $str End If Return $char End Function End Class");
        }

        private class ExpectedTokenInfo
        {
            public int Index { get; set; }
            public string Text { get; set; }
            public TokenType Kind { get; set; }
        }

        private class ExpectedReferenceInfo
        {
            public int Index { get; set; }
            public int NumberOfReferences { get; set; }
        }

        private void CheckCollectedTokens(string extension,
            int totalReferenceCount, IEnumerable<ExpectedReferenceInfo> expectedReferences,
            int totalTokenCount, IEnumerable<ExpectedTokenInfo> expectedTokens,
            string tokenCpdExpected)
        {
            var testFileContent = File.ReadAllLines(TestInputPath + extension);

            CheckTokenInfoFile(testFileContent, extension, totalTokenCount, expectedTokens);
            CheckTokenReferenceFile(testFileContent, extension, totalReferenceCount, expectedReferences);
            CheckCpdTokens(tokenCpdExpected);
        }

        private void CheckCpdTokens(string tokenCpdExpected)
        {
            var cpdInfos = new List<CopyPasteTokenInfo>();

            using (var input = File.OpenRead(Path.Combine(OutputFolderName, Program.CopyPasteTokenInfosFileName)))
            {
                while (input.Position != input.Length)
                {
                    var cpd = new CopyPasteTokenInfo();
                    cpd.MergeDelimitedFrom(input);
                    cpdInfos.Add(cpd);
                }
            }

            Assert.AreEqual(1, cpdInfos.Count);
            var actual = string.Join(" ", cpdInfos[0].TokenInfo.Select(ti => ti.TokenType));
            Assert.AreEqual(tokenCpdExpected, actual);
        }

        private void CheckTokenReferenceFile(string[] testFileContent, string extension,
            int totalReferenceCount, IEnumerable<ExpectedReferenceInfo> expectedReferences)
        {
            var refInfos = new List<FileTokenReferenceInfo>();

            using (var input = File.OpenRead(Path.Combine(OutputFolderName, Program.TokenReferenceInfosFileName)))
            {
                while (input.Position != input.Length)
                {
                    var ri = new FileTokenReferenceInfo();
                    ri.MergeDelimitedFrom(input);
                    refInfos.Add(ri);
                }
            }

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
        private void CheckTokenInfoFile(string[] testInputFileLines, string extension, int totalTokenCount, IEnumerable<ExpectedTokenInfo> expectedTokens)
        {
            var tokenInfos = new List<FileTokenInfo>();

            using (var input = File.OpenRead(Path.Combine(OutputFolderName, Program.TokenInfosFileName)))
            {
                while (input.Position != input.Length)
                {
                    var tokenInfo = new FileTokenInfo();
                    tokenInfo.MergeDelimitedFrom(input);
                    tokenInfos.Add(tokenInfo);
                }
            }

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

        private void CheckExpected(string textActual)
        {
            var expectedContent = new[]
            {
                $@"<AnalysisOutput><Files><File><Path>{TestInputPath}.cs</Path>",
                @"<Metrics><Lines>26</Lines>",
                @"<Issue><Id>S1134</Id><Line>8</Line>",
                @"<Issue><Id>S1135</Id><Line>10</Line>",
                @"<Id>S101</Id><Line>6</Line><Message>Renameclass""TTTestClass""tomatchcamelcasenamingrules,considerusing""TtTestClass"".</Message>",
                @"<Id>S103</Id><Line>20</Line><Message>Splitthis26characterslongline(whichisgreaterthan10authorized).</Message>",
                @"<Id>S103</Id><Line>23</Line><Message>Splitthis19characterslongline(whichisgreaterthan10authorized).</Message>",
                @"<Id>S104</Id><Line>1</Line><Message>Thisfilehas26lines,whichisgreaterthan10authorized.Splititintosmallerfiles.</Message>"
            };

            foreach (var expected in expectedContent)
            {
                if (!textActual.Contains(expected))
                {
                    Assert.Fail("Generated output file doesn't contain expected string '{0}'", expected);
                }
            }
        }

        private static void CheckNotExpected(string textActual)
        {
            var notExpectedContent = new[]
            {
                @"<Id>S1116</Id><Line>15</Line>"
            };

            foreach (var notExpected in notExpectedContent)
            {
                if (textActual.Contains(notExpected))
                {
                    Assert.Fail("Generated output file contains not expected string '{0}'", notExpected);
                }
            }
        }
    }
}
