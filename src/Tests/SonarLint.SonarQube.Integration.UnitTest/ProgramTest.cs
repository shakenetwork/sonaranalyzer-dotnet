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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Common;
using SonarLint.Helpers;
using SonarLint.Runner;
using System.IO;
using System.Linq;
using SonarAnalyzer.Protobuf;
using Google.Protobuf;
using System.Collections.Generic;

namespace SonarLint.UnitTest
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
                new ExpectedTokenInfo
                {
                    TokenCount = 34,
                    DeclarationIndex = 2,
                    ReferenceCount = 3,
                    ReferenceIndex = 2
                });
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
                new ExpectedTokenInfo
                {
                    TokenCount = 31,
                    DeclarationIndex = 2,
                    ReferenceCount = 3,
                    ReferenceIndex = 2
                });
        }

        private class ExpectedTokenInfo
        {
            public int TokenCount { get; set; }
            public int DeclarationIndex { get; set; }
            public int ReferenceCount { get; internal set; }
            public int ReferenceIndex { get; internal set; }
        }

        private void CheckCollectedTokens(string extension, ExpectedTokenInfo expectedTokenInfo)
        {
            var testFileContent = File.ReadAllLines(TestInputPath + extension);

            CheckTokenInfoFile(testFileContent, extension, expectedTokenInfo);
            CheckTokenReferenceFile(testFileContent, extension, expectedTokenInfo);
        }

        private void CheckTokenReferenceFile(string[] testInputFileLines, string extension, ExpectedTokenInfo expectedTokenInfo)
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
            Assert.AreEqual(expectedTokenInfo.ReferenceCount, refInfo.Reference.Count);

            var declarationPosition = refInfo.Reference[expectedTokenInfo.ReferenceIndex].Declaration;
            Assert.AreEqual(declarationPosition.StartLine, declarationPosition.EndLine);
            var tokenText = testInputFileLines[declarationPosition.StartLine - 1].Substring(
                declarationPosition.StartOffset,
                declarationPosition.EndOffset - declarationPosition.StartOffset);
            Assert.AreEqual("x", tokenText);

            Assert.AreEqual(1, refInfo.Reference[2].Reference.Count);
            var referencePosition = refInfo.Reference[2].Reference[0];
            Assert.AreEqual(referencePosition.StartLine, referencePosition.EndLine);
            tokenText = testInputFileLines[referencePosition.StartLine - 1].Substring(
                referencePosition.StartOffset,
                referencePosition.EndOffset - referencePosition.StartOffset);
            Assert.AreEqual("x", tokenText);
        }

        private void CheckTokenInfoFile(string[] testInputFileLines, string extension, ExpectedTokenInfo expectedTokenInfo)
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
            Assert.AreEqual(expectedTokenInfo.TokenCount, token.TokenInfo.Count);
            Assert.AreEqual(TokenType.DeclarationName, token.TokenInfo[expectedTokenInfo.DeclarationIndex].TokenType);

            var tokenPosition = token.TokenInfo[expectedTokenInfo.DeclarationIndex].TextRange;
            Assert.AreEqual(tokenPosition.StartLine, tokenPosition.EndLine);
            var tokenText = testInputFileLines[tokenPosition.StartLine - 1].Substring(
                tokenPosition.StartOffset,
                tokenPosition.EndOffset - tokenPosition.StartOffset);
            Assert.AreEqual("TTTestClass", tokenText);
        }

        private void CheckExpected(string textActual)
        {
            var expectedContent = new[]
            {
                $@"<AnalysisOutput><Files><File><Path>{TestInputPath}.cs</Path>",
                @"<Metrics><Lines>17</Lines>",
                @"<Issue><Id>S1134</Id><Line>3</Line>",
                @"<Issue><Id>S1135</Id><Line>5</Line>",
                @"<Id>S101</Id><Line>1</Line><Message>Renameclass""TTTestClass""tomatchcamelcasenamingrules,considerusing""TtTestClass"".</Message>",
                @"<Id>S103</Id><Line>11</Line><Message>Splitthis21characterslongline(whichisgreaterthan10authorized).</Message>",
                @"<Id>S103</Id><Line>14</Line><Message>Splitthis17characterslongline(whichisgreaterthan10authorized).</Message>",
                @"<Id>S104</Id><Line>1</Line><Message>Thisfilehas17lines,whichisgreaterthan10authorized.Splititintosmallerfiles.</Message>"
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
                @"<Id>S1116</Id><Line>14</Line>"
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
