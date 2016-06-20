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
using SonarLint.DocGenerator;
using SonarLint.Utilities;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonarLint.UnitTest.PackagingTests
{
    [TestClass]
    public class DocGeneratorTest
    {
        private const string TestCategoryName = "DocGenerator";

        [TestMethod]
        [TestCategory(TestCategoryName)]
        public void CheckNumberOfCrossReferences()
        {
            var crossReferenceCount = GetNumberOfCrossReferences(AnalyzerLanguage.CSharp);
            Assert.AreEqual(4, crossReferenceCount);
            crossReferenceCount = GetNumberOfCrossReferences(AnalyzerLanguage.VisualBasic);
            Assert.AreEqual(0, crossReferenceCount);
        }

        [TestMethod]
        [TestCategory(TestCategoryName)]
        public void CheckNumberOfCrossLinks()
        {
            var crossReferenceCount = GetNumberOfCrossReferences(AnalyzerLanguage.VisualBasic) + GetNumberOfCrossReferences(AnalyzerLanguage.CSharp);
            var productVersion = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion;
            var json = Program.GenerateRuleJson(productVersion);

            var commonSubUrl = RuleImplementationMeta.HelpLinkPattern.Replace("{1}", string.Empty);
            var crossLinkCount = NumberOfOccurrences(json, string.Format(commonSubUrl, productVersion));

            Assert.AreEqual(crossReferenceCount, crossLinkCount);
        }

        private static int GetNumberOfCrossReferences(AnalyzerLanguage language)
        {
            return RuleDetailBuilder.GetParameterlessRuleDetails(language)
                .Select(rule => rule.Description)
                .Select(description => Regex.Matches(description, RuleImplementationMeta.CrosslinkPattern).Count)
                .Sum();
        }

        private static int NumberOfOccurrences(string source, string substring)
        {
            var replaced = source.Replace(substring, string.Empty);
            return (source.Length - replaced.Length) / substring.Length;
        }
    }
}
