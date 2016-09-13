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

using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Runner;
using SonarLint.Helpers;
using System.IO;

namespace SonarLint.UnitTest
{
    [TestClass]
    public class ConfigurationTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Configuration()
        {
            var tempInputFilePath = Path.Combine(TestContext.DeploymentDirectory, ParameterLoader.ParameterConfigurationFileName);
            File.Copy(Path.Combine(ProgramTest.TestResourcesFolderName, "SonarLint.Cs.xml"), tempInputFilePath, true);

            var conf = new Configuration(
                tempInputFilePath,
                Common.AnalyzerLanguage.CSharp);

            conf.IgnoreHeaderComments.Should().BeTrue();
            conf.Files.Should().BeEquivalentTo(ProgramTest.TestInputPath +".cs");

            string[] expectedAnalyzerIds =
            {
                "S1121",
                "S2306",
                "S1227",

                "S104",
                "S1541",
                "S103",
                "S1479",
                "S1067",
                "S107",
                "S101",
                "S100",
                "S1134",
                "S1135"
            };

            conf.AnalyzerIds.Should().BeEquivalentTo(expectedAnalyzerIds);

            var analyzers = conf.GetAnalyzers();
            Assert.AreEqual(expectedAnalyzerIds.Length, analyzers.Count());
        }
    }
}
